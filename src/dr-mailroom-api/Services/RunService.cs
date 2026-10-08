using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using dr_mailroom_core.Runs;

namespace dr_mailroom_api.Services;

/// <summary>Servizio dei giri: avvio immediato con 409 su casella occupata (D11, D16, D17), annullamento e consultazione.</summary>
public class RunService(
    RunProvider runProvider,
    MailboxProvider mailboxProvider,
    IMailboxLock mailboxLock,
    IRunDispatcher dispatcher,
    RunCancellationRegistry cancellationRegistry,
    ILogger<RunService> logger)
{
    /// <summary>Giri che rispettano il filtro, DTO completo.</summary>
    public Task<List<RunDto>> GetAllAsync(RunFilter filter, CancellationToken cancellationToken) =>
        runProvider.GetRunAsync(filter, RunDto.Projection, cancellationToken);

    /// <summary>Giri che rispettano il filtro, DTO ridotto.</summary>
    public Task<List<RunSummaryDto>> GetSummariesAsync(RunFilter filter, CancellationToken cancellationToken) =>
        runProvider.GetRunAsync(filter, RunSummaryDto.Projection, cancellationToken);

    /// <summary>Un giro, null se non esiste.</summary>
    public Task<RunDto?> GetByIdAsync(long runId, CancellationToken cancellationToken) =>
        runProvider.GetRunByIdAsync(runId, RunDto.Projection, cancellationToken);

    /// <summary>Giro manuale su una casella attiva o, senza casella, su tutte le caselle attive.</summary>
    public async Task<ServiceResult<RunsStartedDto>> StartManualAsync(StartRunRequest request, CancellationToken cancellationToken)
    {
        if (request.MailboxId is int mailboxId)
        {
            // Leggo da DB lo stato della casella richiesta
            var status = await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken);
            if (status is null)
            {
                return ServiceResult<RunsStartedDto>.Invalid("mailboxId", ServiceMessages.MailboxNotFound);
            }

            if (status != MailboxStatus.Active)
            {
                return ServiceResult<RunsStartedDto>.Conflict(ServiceMessages.MailboxNotActive);
            }

            return await StartRunsAsync([mailboxId], RunTrigger.Manual, null, null, cancellationToken);
        }

        // Leggo da DB tutte le caselle attive
        var active = await mailboxProvider.GetActiveMailboxIdsAsync(cancellationToken);
        return await StartRunsAsync(active, RunTrigger.Manual, null, null, cancellationToken);
    }

    /// <summary>
    /// Crea e affida allo scheduler un giro per ogni casella libera; le occupate (giro Running o lock preso) si saltano.
    /// Conflict se non c'è nessuna casella o se sono tutte occupate.
    /// </summary>
    public async Task<ServiceResult<RunsStartedDto>> StartRunsAsync(
        IReadOnlyList<int> mailboxIds,
        RunTrigger trigger,
        int? filterId,
        string? requestedProcesses,
        CancellationToken cancellationToken)
    {
        if (mailboxIds.Count == 0)
        {
            return ServiceResult<RunsStartedDto>.Conflict(ServiceMessages.NoActiveMailboxes);
        }

        var started = new List<long>();
        var skipped = new List<int>();
        foreach (var mailboxId in mailboxIds)
        {
            if (await IsBusyAsync(mailboxId, cancellationToken))
            {
                skipped.Add(mailboxId);
                continue;
            }

            // Scrivo su DB il giro, poi lo affido a Quartz per l'avvio immediato
            var runId = await runProvider.CreateRunAsync(mailboxId, trigger, filterId, requestedProcesses, cancellationToken);
            await dispatcher.DispatchAsync(runId, cancellationToken);
            started.Add(runId);
        }

        logger.LogInformation(
            "Avviati {Started} giri {Trigger}, {Skipped} caselle saltate perché occupate",
            started.Count, trigger, skipped.Count);

        if (started.Count == 0)
        {
            return ServiceResult<RunsStartedDto>.Conflict(mailboxIds.Count == 1 ? ServiceMessages.MailboxBusy : ServiceMessages.AllMailboxesBusy);
        }

        return ServiceResult<RunsStartedDto>.Ok(new RunsStartedDto(started, skipped));
    }

    /// <summary>Annulla un giro in corso: 404 se non esiste, 409 se è già chiuso. Il giro chiude Cancelled in modo asincrono.</summary>
    public async Task<ServiceResult<RunsCancelledDto>> CancelAsync(long runId, CancellationToken cancellationToken)
    {
        // Leggo da DB il giro da annullare
        var run = await runProvider.GetRunByIdAsync(runId, RunSummaryDto.Projection, cancellationToken);
        if (run is null)
        {
            return ServiceResult<RunsCancelledDto>.NotFound(ServiceMessages.RunNotFound);
        }

        if (run.Status != RunStatus.Running)
        {
            return ServiceResult<RunsCancelledDto>.Conflict(ServiceMessages.RunNotRunning);
        }

        await CancelRunAsync(runId, cancellationToken);
        return ServiceResult<RunsCancelledDto>.Ok(new RunsCancelledDto([runId]));
    }

    /// <summary>Annulla tutti i giri in corso: 409 se non ce n'è nessuno.</summary>
    public async Task<ServiceResult<RunsCancelledDto>> CancelAllAsync(CancellationToken cancellationToken)
    {
        var cancelled = await CancelRunningRunsAsync(null, cancellationToken);
        return cancelled.Count == 0
            ? ServiceResult<RunsCancelledDto>.Conflict(ServiceMessages.NoRunningRuns)
            : ServiceResult<RunsCancelledDto>.Ok(new RunsCancelledDto(cancelled));
    }

    /// <summary>Annulla i giri in corso di una casella o di tutte (mailboxId null); restituisce gli Id.</summary>
    public async Task<List<long>> CancelRunningRunsAsync(int? mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB i giri in corso
        var runIds = await runProvider.GetRunningRunIdsAsync(mailboxId, cancellationToken);
        foreach (var runId in runIds)
        {
            await CancelRunAsync(runId, cancellationToken);
        }

        logger.LogInformation("Annullamento richiesto per {Count} giri (casella {MailboxId})", runIds.Count, mailboxId);
        return runIds;
    }

    /// <summary>
    /// Giro in esecuzione qui: annullo il suo token e lo chiude l'orchestratore.
    /// Altrimenti (non ancora partito o orfano) lo chiudo su DB e riprovo il token, nel caso sia partito nel frattempo.
    /// </summary>
    private async Task CancelRunAsync(long runId, CancellationToken cancellationToken)
    {
        if (cancellationRegistry.TryCancel(runId))
        {
            return;
        }

        // Scrivo su DB la chiusura del giro senza job in esecuzione
        await runProvider.CancelRunIfRunningAsync(runId, RunOrchestrator.RunCancelledMessage, cancellationToken);
        cancellationRegistry.TryCancel(runId);
    }

    /// <summary>Occupata se ha un giro Running o se il lock SQL è preso (sonda: preso e subito rilasciato).</summary>
    private async Task<bool> IsBusyAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB se esiste un giro in corso per la casella
        if (await runProvider.HasRunningRunAsync(mailboxId, cancellationToken))
        {
            return true;
        }

        // Sondo il lock SQL della casella
        var handle = await mailboxLock.TryAcquireAsync(mailboxId, cancellationToken);
        if (handle is null)
        {
            return true;
        }

        await handle.DisposeAsync();
        return false;
    }
}
