using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Ingestion;
using dr_mailroom_core.Processes;
using dr_mailroom_core.Security;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Runs;

/// <summary>
/// Esegue un giro su una casella: lock → ingestione (produttore) → coda FIFO → filtri e processi in parallelo (giro normale),
/// oppure lock → messaggi già indicizzati del filtro (produttore) → coda → processi richiesti (riapplica, D13).
/// Una sola sessione Gmail per giro, condivisa da produttore e consumatori: un solo limitatore di quota per casella.
/// Non rilancia mai, salvo cancellazione: l'esito finisce su Run e Mailbox.
/// </summary>
public class RunOrchestrator(
    IMailboxLock mailboxLock,
    MailboxIngestionService ingestionService,
    RunProvider runProvider,
    MailboxProvider mailboxProvider,
    FilterProvider filterProvider,
    MessageProvider messageProvider,
    MessageProcessingQueue processingQueue,
    IGmailReader gmailReader,
    ITokenProtector tokenProtector,
    ILogger<RunOrchestrator> logger)
{
    /// <summary>Messaggio registrato sul giro quando la casella è occupata.</summary>
    public const string MailboxBusyMessage = "Casella occupata da un altro giro";

    /// <summary>Messaggio registrato sul giro quando la casella è eliminata (D18).</summary>
    public const string MailboxDeletedMessage = "Casella eliminata: nessun giro";

    /// <summary>Separatore dei processi in Run.RequestedProcesses.</summary>
    public const char ProcessSeparator = ',';

    /// <summary>Esegue il giro indicato e ne registra l'esito.</summary>
    public async Task ExecuteAsync(long runId, CancellationToken cancellationToken)
    {
        // Leggo da DB il giro da eseguire
        var run = await runProvider.GetRunForUpdateAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Giro {runId} inesistente");

        if (run.Status != RunStatus.Running)
        {
            logger.LogWarning("Giro {RunId} già chiuso ({Status}): non lo eseguo", runId, run.Status);
            return;
        }

        // Leggo da DB lo stato della casella: una casella eliminata non si lavora più (D18)
        if (await mailboxProvider.GetMailboxStatusAsync(run.MailboxId, cancellationToken) == MailboxStatus.Deleted)
        {
            await runProvider.FinishRunAsync(runId, RunStatus.Skipped, null, 0, 0, MailboxDeletedMessage, cancellationToken);
            return;
        }

        // Prendo il lock della casella su SQL Server
        await using var handle = await mailboxLock.TryAcquireAsync(run.MailboxId, cancellationToken);
        if (handle is null)
        {
            logger.LogInformation("Giro {RunId}: casella {MailboxId} occupata, giro saltato", runId, run.MailboxId);
            await runProvider.FinishRunAsync(runId, RunStatus.Skipped, null, 0, 0, MailboxBusyMessage, cancellationToken);
            return;
        }

        var mailboxId = run.MailboxId;
        try
        {
            var outcome = run.Trigger == RunTrigger.Reapply
                ? await ReapplyAsync(run, cancellationToken)
                : await IngestAndProcessAsync(run, cancellationToken);

            await runProvider.FinishRunAsync(
                runId, RunStatus.Completed, outcome.MessagesRead, outcome.Processed, outcome.Errors, outcome.ErrorSummary, cancellationToken);
            await mailboxProvider.RecordRunOutcomeAsync(mailboxId, null, null, cancellationToken);
        }
        catch (TokenRevokedException ex)
        {
            logger.LogError(ex, "Giro {RunId}: refresh token della casella {MailboxId} revocato", runId, mailboxId);
            await runProvider.FinishRunAsync(runId, RunStatus.Failed, null, 0, 1, ex.Message, cancellationToken);
            await mailboxProvider.RecordRunOutcomeAsync(mailboxId, ex.Message, MailboxStatus.TokenRevoked, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Giro {RunId} sulla casella {MailboxId} fallito", runId, mailboxId);
            await runProvider.FinishRunAsync(runId, RunStatus.Failed, null, 0, 1, ex.Message, cancellationToken);
            await mailboxProvider.RecordRunOutcomeAsync(mailboxId, ex.Message, null, cancellationToken);
        }
    }

    /// <summary>Giro normale: l'ingestione è il produttore; fuori dal backfill (D12) i messaggi nuovi passano dalla coda a filtri e processi.</summary>
    private async Task<RunOutcome> IngestAndProcessAsync(Run run, CancellationToken cancellationToken)
    {
        var mailbox = await GetConnectedMailboxAsync(run.MailboxId, cancellationToken);
        using var session = gmailReader.OpenSession(tokenProtector.Unprotect(mailbox.EncryptedRefreshToken!));

        // Leggo da DB i filtri attivi: nessun filtro, o backfill in corso, → nessun messaggio da elaborare
        IReadOnlyList<Filter> filters = mailbox.BackfillCompleted
            ? await filterProvider.GetActiveFiltersForMailboxAsync(run.MailboxId, cancellationToken)
            : [];

        if (filters.Count == 0)
        {
            await ingestionService.IngestAsync(run.MailboxId, run.Id, session, null, cancellationToken);
            return new RunOutcome(null, 0, 0, null);
        }

        var outcome = await processingQueue.RunAsync(
            session,
            filters,
            null,
            (writer, ct) => ingestionService.IngestAsync(run.MailboxId, run.Id, session, writer, ct),
            cancellationToken);
        return ToRunOutcome(null, outcome);
    }

    /// <summary>Riapplica: i messaggi già indicizzati del mittente del filtro passano dalla coda, con il solo sottoinsieme di processi richiesto.</summary>
    private async Task<RunOutcome> ReapplyAsync(Run run, CancellationToken cancellationToken)
    {
        var filterId = run.FilterId ?? throw new InvalidOperationException($"Giro di riapplica {run.Id} senza filtro");

        // Leggo da DB il filtro con i suoi processi
        var filter = await filterProvider.GetFilterWithProcessesAsync(filterId, cancellationToken)
            ?? throw new InvalidOperationException($"Filtro {filterId} inesistente");

        // La riapplica è una richiesta esplicita: vale anche per un filtro disattivato (entità non tracciata, nessun salvataggio)
        filter.IsEnabled = true;

        var filterProcesses = filter.Processes.Select(p => p.ProcessType).ToHashSet();
        var requested = ParseProcesses(run.RequestedProcesses) ?? filterProcesses;
        requested.IntersectWith(filterProcesses);

        // Leggo da DB i candidati: stesso mittente, la condizione sull'oggetto la valuta il motore dei filtri
        var candidates = await messageProvider.GetMessagesBySenderAsync(run.MailboxId, filter.SenderAddress, cancellationToken);
        if (candidates.Count == 0)
        {
            return new RunOutcome(0, 0, 0, null);
        }

        var mailbox = await GetConnectedMailboxAsync(run.MailboxId, cancellationToken);
        using var session = gmailReader.OpenSession(tokenProtector.Unprotect(mailbox.EncryptedRefreshToken!));

        var outcome = await processingQueue.RunAsync(
            session,
            [filter],
            requested,
            async (writer, ct) =>
            {
                foreach (var candidate in candidates)
                {
                    await writer.WriteAsync(candidate.Id, ct);
                }
            },
            cancellationToken);
        return ToRunOutcome(candidates.Count, outcome);
    }

    /// <summary>Legge la casella e verifica che sia collegata (refresh token presente).</summary>
    private async Task<Mailbox> GetConnectedMailboxAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB la casella per il refresh token cifrato
        var mailbox = await mailboxProvider.GetMailboxForUpdateAsync(mailboxId, cancellationToken)
            ?? throw new InvalidOperationException($"Casella {mailboxId} inesistente");

        if (string.IsNullOrEmpty(mailbox.EncryptedRefreshToken))
        {
            throw new InvalidOperationException($"Casella {mailboxId} non collegata: refresh token assente");
        }

        return mailbox;
    }

    /// <summary>Converte l'esito della coda nei contatori del giro.</summary>
    private static RunOutcome ToRunOutcome(int? messagesRead, QueueOutcome outcome)
    {
        var summary = outcome.Errors > 0 ? $"{outcome.Errors} messaggi con processi falliti: dettaglio in ProcessExecution" : null;
        return new RunOutcome(messagesRead, outcome.Processed, outcome.Errors, summary);
    }

    /// <summary>Processi richiesti dalla riapplica (testo separato da virgole); null = tutti quelli del filtro.</summary>
    private static HashSet<ProcessType>? ParseProcesses(string? requested) =>
        string.IsNullOrWhiteSpace(requested)
            ? null
            : requested.Split(ProcessSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Enum.Parse<ProcessType>)
                .ToHashSet();

    /// <summary>Contatori del giro; MessagesRead null = lasciare quello aggiornato dall'ingestione.</summary>
    private sealed record RunOutcome(int? MessagesRead, int Processed, int Errors, string? ErrorSummary);
}
