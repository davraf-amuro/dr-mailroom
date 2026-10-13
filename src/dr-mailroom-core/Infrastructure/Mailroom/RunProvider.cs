using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati ai giri: creazione, contatori ed esito finale.</summary>
public class RunProvider(
    MailroomDbContext context,
    IDbContextFactory<MailroomDbContext> contextFactory,
    ILogger<RunProvider> logger)
{
    /// <summary>Carica il giro con tracking, per aggiornarne i contatori insieme al checkpoint.</summary>
    public async Task<Run?> GetRunForUpdateAsync(long runId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Carico il giro {RunId} per aggiornamento", runId);

        // Leggo da DB il giro con tracking
        return await context.Runs.AsTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
    }

    /// <summary>Crea un giro in stato Running e ne restituisce l'Id; senderAddress solo per il cestino di un mittente.</summary>
    public async Task<long> CreateRunAsync(
        int mailboxId,
        RunTrigger trigger,
        int? filterId,
        string? requestedProcesses,
        string? senderAddress,
        CancellationToken cancellationToken)
    {
        var run = new Run
        {
            MailboxId = mailboxId,
            Trigger = trigger,
            FilterId = filterId,
            RequestedProcesses = requestedProcesses,
            SenderAddress = senderAddress,
            Status = RunStatus.Running,
            StartedAtUtc = DateTime.UtcNow,
        };
        context.Runs.Add(run);

        // Scrivo su DB il nuovo giro
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Creato giro {RunId} ({Trigger}) per la casella {MailboxId}", run.Id, trigger, mailboxId);
        return run.Id;
    }

    /// <summary>True se la casella ha un giro ancora in corso.</summary>
    public async Task<bool> HasRunningRunAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB se esiste un giro Running per la casella
        return await context.Runs.AnyAsync(r => r.MailboxId == mailboxId && r.Status == RunStatus.Running, cancellationToken);
    }

    /// <summary>Id dei giri Running, di una casella o di tutte (mailboxId null), dal più vecchio.</summary>
    public async Task<List<long>> GetRunningRunIdsAsync(int? mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB gli Id dei giri in corso
        return await context.Runs
            .Where(r => r.Status == RunStatus.Running && (mailboxId == null || r.MailboxId == mailboxId))
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Chiude come Cancelled un giro ancora Running: annullato durante l'esecuzione, oppure senza job (non partito o orfano).
    /// False se il giro è già chiuso.
    /// </summary>
    public async Task<bool> CancelRunIfRunningAsync(long runId, string reason, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Leggo da DB il giro con tracking, solo se è ancora in corso
        var run = await db.Runs.AsTracking()
            .FirstOrDefaultAsync(r => r.Id == runId && r.Status == RunStatus.Running, cancellationToken);
        if (run is null)
        {
            return false;
        }

        run.Status = RunStatus.Cancelled;
        run.EndedAtUtc = DateTime.UtcNow;
        run.ErrorSummary = TextLimits.Truncate(reason, ColumnLengths.ErrorText);

        // Scrivo su DB la chiusura del giro
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Giro {RunId} chiuso: {Status}", runId, RunStatus.Cancelled);
        return true;
    }

    /// <summary>Chiude come Failed i giri rimasti Running dopo un arresto non pulito; restituisce quanti.</summary>
    public async Task<int> FailInterruptedRunsAsync(string reason, CancellationToken cancellationToken)
    {
        // Leggo da DB i giri rimasti in corso, con tracking per chiuderli
        var interrupted = await context.Runs.AsTracking()
            .Where(r => r.Status == RunStatus.Running)
            .ToListAsync(cancellationToken);

        foreach (var run in interrupted)
        {
            run.Status = RunStatus.Failed;
            run.EndedAtUtc = DateTime.UtcNow;
            run.ErrorSummary = reason;
        }

        // Scrivo su DB la chiusura dei giri interrotti
        await context.SaveChangesAsync(cancellationToken);
        return interrupted.Count;
    }

    /// <summary>
    /// Scrive l'esito finale del giro con un contesto dedicato: le modifiche pendenti (e magari fallite)
    /// del contesto del giro non vengono salvate per sbaglio.
    /// </summary>
    public async Task FinishRunAsync(
        long runId,
        RunStatus status,
        int? messagesRead,
        int messagesProcessed,
        int errorCount,
        string? errorSummary,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Leggo da DB il giro con tracking, in un contesto pulito
        var run = await db.Runs.AsTracking().FirstAsync(r => r.Id == runId, cancellationToken);
        run.Status = status;
        run.EndedAtUtc = DateTime.UtcNow;
        run.MessagesRead = messagesRead ?? run.MessagesRead;
        run.MessagesProcessed = messagesProcessed;
        run.ErrorCount = errorCount;
        run.ErrorSummary = TextLimits.Truncate(errorSummary, ColumnLengths.ErrorText);

        // Scrivo su DB l'esito del giro
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Giro {RunId} chiuso: {Status}, {Processed} messaggi lavorati, {Errors} errori",
            runId, status, messagesProcessed, errorCount);
    }

    /// <summary>
    /// Aggiorna i contatori di un giro in corso (avanzamento visibile durante il giro), con un contesto dedicato.
    /// Non tocca stato ed esito: li scrive FinishRunAsync.
    /// </summary>
    public async Task UpdateProgressAsync(long runId, int messagesRead, int messagesProcessed, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Leggo da DB il giro con tracking, in un contesto pulito
        var run = await db.Runs.AsTracking().FirstAsync(r => r.Id == runId, cancellationToken);
        run.MessagesRead = messagesRead;
        run.MessagesProcessed = messagesProcessed;

        // Scrivo su DB l'avanzamento
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Giri che rispettano il filtro, dal più recente, proiettati con il selector EF-traducibile.</summary>
    public async Task<List<TResult>> GetRunAsync<TResult>(
        RunFilter filter,
        Expression<Func<Run, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Elenco giri con filtro {@Filter}", filter);

        // Leggo da DB: WHERE con i soli filtri valorizzati, SELECT con le sole colonne del DTO
        return await context.Runs
            .Where(filter.ToExpression())
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Un giro proiettato, null se non esiste.</summary>
    public async Task<TResult?> GetRunByIdAsync<TResult>(
        long runId,
        Expression<Func<Run, TResult>> selector,
        CancellationToken cancellationToken)
    {
        // Leggo da DB il giro richiesto
        return await context.Runs
            .Where(r => r.Id == runId)
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
