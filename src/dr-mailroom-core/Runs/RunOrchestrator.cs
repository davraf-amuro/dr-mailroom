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
/// oppure lock → messaggi già indicizzati del filtro (produttore) → coda → processi richiesti (riapplica, D13),
/// oppure lock → mail del mittente → cestino di Gmail a blocchi con il token separato (cestino, D-T1 bis).
/// Una sola sessione Gmail per giro, condivisa da produttore e consumatori: un solo limitatore di quota per casella.
/// Non rilancia mai: l'esito finisce su Run e Mailbox. Un giro annullato (API o arresto del servizio) chiude Cancelled.
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
    IGmailTrasher gmailTrasher,
    ITokenProtector tokenProtector,
    RunCancellationRegistry cancellationRegistry,
    ILogger<RunOrchestrator> logger)
{
    /// <summary>Messaggio registrato sul giro quando la casella è occupata.</summary>
    public const string MailboxBusyMessage = "Casella occupata da un altro giro";

    /// <summary>Messaggio registrato sul giro quando la casella è eliminata (D18).</summary>
    public const string MailboxDeletedMessage = "Casella eliminata: nessun giro";

    /// <summary>Messaggio registrato sul giro quando la casella è in pausa (Disabled).</summary>
    public const string MailboxPausedMessage = "Casella in pausa: nessun giro";

    /// <summary>Messaggio registrato sul giro annullato via API.</summary>
    public const string RunCancelledMessage = "Giro annullato su richiesta";

    /// <summary>Messaggio registrato sul giro annullato dall'arresto del servizio.</summary>
    public const string ServiceStoppingMessage = "Giro annullato dall'arresto del servizio";

    /// <summary>Messaggio registrato sul giro di cestino quando la casella non ha il token del cestino.</summary>
    public const string TrashNotAuthorizedMessage = "Cestino non autorizzato per la casella: autorizzalo dalla vista del mittente";

    /// <summary>Messaggio registrato sul giro di cestino quando Google revoca il token del cestino (la lettura non cambia).</summary>
    public const string TrashTokenRevokedMessage = "Autorizzazione del cestino revocata da Google: va ripetuta";

    /// <summary>Separatore dei processi in Run.RequestedProcesses.</summary>
    public const char ProcessSeparator = ',';

    /// <summary>Esegue il giro indicato e ne registra l'esito; annullabile tramite RunCancellationRegistry.</summary>
    public async Task ExecuteAsync(long runId, CancellationToken cancellationToken)
    {
        // Registro il giro prima di leggerne lo stato: un annullamento arrivato nel frattempo non va perso
        using var registration = cancellationRegistry.Register(runId, cancellationToken);
        var runToken = registration.Token;
        try
        {
            await ExecuteRegisteredAsync(runId, runToken);
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            var reason = cancellationRegistry.IsShuttingDown ? ServiceStoppingMessage : RunCancelledMessage;
            logger.LogInformation("Giro {RunId} annullato: {Reason}", runId, reason);

            // Scrivo su DB la chiusura Cancelled, se nessuno l'ha già chiuso; il token del giro è annullato, quindi None
            await runProvider.CancelRunIfRunningAsync(runId, reason, CancellationToken.None);
        }
    }

    /// <summary>Corpo del giro, con il token del registro.</summary>
    private async Task ExecuteRegisteredAsync(long runId, CancellationToken cancellationToken)
    {
        // Leggo da DB il giro da eseguire
        var run = await runProvider.GetRunForUpdateAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Giro {runId} inesistente");

        if (run.Status != RunStatus.Running)
        {
            logger.LogWarning("Giro {RunId} già chiuso ({Status}): non lo eseguo", runId, run.Status);
            return;
        }

        // Leggo da DB lo stato della casella: eliminata (D18) o in pausa non si lavora
        var mailboxStatus = await mailboxProvider.GetMailboxStatusAsync(run.MailboxId, cancellationToken);
        if (mailboxStatus is MailboxStatus.Deleted or MailboxStatus.Disabled)
        {
            var reason = mailboxStatus == MailboxStatus.Deleted ? MailboxDeletedMessage : MailboxPausedMessage;
            await runProvider.FinishRunAsync(runId, RunStatus.Skipped, null, 0, 0, reason, cancellationToken);
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

        // Il cestino ha un esito tutto suo: non è un giro di lettura e non tocca LastRunAtUtc né LastError della casella
        if (run.Trigger == RunTrigger.TrashSender)
        {
            await TrashSenderAsync(run, cancellationToken);
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
            await mailboxProvider.RecordRunOutcomeAsync(mailboxId, runId, null, null, cancellationToken);
        }
        catch (TokenRevokedException ex)
        {
            logger.LogError(ex, "Giro {RunId}: refresh token della casella {MailboxId} revocato", runId, mailboxId);
            await runProvider.FinishRunAsync(runId, RunStatus.Failed, null, 0, 1, ex.Message, cancellationToken);
            await mailboxProvider.RecordRunOutcomeAsync(mailboxId, runId, ex.Message, MailboxStatus.TokenRevoked, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Giro {RunId} sulla casella {MailboxId} fallito", runId, mailboxId);
            await runProvider.FinishRunAsync(runId, RunStatus.Failed, null, 0, 1, ex.Message, cancellationToken);
            await mailboxProvider.RecordRunOutcomeAsync(mailboxId, runId, ex.Message, null, cancellationToken);
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

    /// <summary>
    /// Cestino di un mittente: le mail ricevute e non ancora cestinate vanno nel cestino di Gmail a blocchi di
    /// <see cref="GmailConstants.MaxBatchModifyIds"/>; ogni blocco riuscito si marca subito nel DB e aggiorna i contatori
    /// (MessagesRead = trovate, MessagesProcessed = cestinate). Un annullamento si ferma tra un blocco e l'altro.
    /// invalid_grant azzera il solo token del cestino: la casella resta leggibile.
    /// </summary>
    private async Task TrashSenderAsync(Run run, CancellationToken cancellationToken)
    {
        var sender = run.SenderAddress ?? throw new InvalidOperationException($"Giro di cestino {run.Id} senza mittente");
        var found = 0;
        var trashed = 0;
        try
        {
            // Leggo da DB il token del cestino: senza autorizzazione il giro non tocca Gmail
            var encryptedTrashToken = await mailboxProvider.GetTrashTokenAsync(run.MailboxId, cancellationToken);
            if (string.IsNullOrEmpty(encryptedTrashToken))
            {
                logger.LogWarning("Giro {RunId}: cestino non autorizzato per la casella {MailboxId}", run.Id, run.MailboxId);
                await runProvider.FinishRunAsync(run.Id, RunStatus.Failed, 0, 0, 1, TrashNotAuthorizedMessage, cancellationToken);
                return;
            }

            // Leggo da DB le mail del mittente ancora da cestinare
            var candidates = await messageProvider.GetTrashCandidatesAsync(
                run.MailboxId, sender, run.TrashReceivedBeforeUtc, run.TrashWithoutAttachmentsOnly, cancellationToken);
            found = candidates.Count;

            using var session = gmailTrasher.OpenSession(tokenProtector.Unprotect(encryptedTrashToken));
            foreach (var chunk in candidates.Chunk(GmailConstants.MaxBatchModifyIds))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await session.TrashAsync([.. chunk.Select(c => c.GmailMessageId)], cancellationToken);

                // Scrivo su DB: blocco marcato cestinato e avanzamento del giro. Senza token: Gmail l'ha già cestinato,
                // un annullamento arrivato nel frattempo non deve lasciare il DB indietro rispetto a Gmail
                await messageProvider.MarkTrashedAsync([.. chunk.Select(c => c.Id)], DateTime.UtcNow, CancellationToken.None);
                trashed += chunk.Length;
                await runProvider.UpdateProgressAsync(run.Id, found, trashed, CancellationToken.None);
            }

            logger.LogInformation(
                "Giro {RunId}: {Trashed} mail su {Found} spostate nel cestino nella casella {MailboxId}", run.Id, trashed, found, run.MailboxId);
            await runProvider.FinishRunAsync(run.Id, RunStatus.Completed, found, trashed, 0, null, cancellationToken);
        }
        catch (TokenRevokedException ex)
        {
            logger.LogError(ex, "Giro {RunId}: token del cestino della casella {MailboxId} revocato", run.Id, run.MailboxId);

            // Scrivo su DB: via il solo token del cestino, quello di lettura resta
            await mailboxProvider.SetTrashTokenAsync(run.MailboxId, null, cancellationToken);
            await runProvider.FinishRunAsync(run.Id, RunStatus.Failed, found, trashed, 1, TrashTokenRevokedMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Giro di cestino {RunId} sulla casella {MailboxId} fallito", run.Id, run.MailboxId);
            await runProvider.FinishRunAsync(run.Id, RunStatus.Failed, found, trashed, 1, ex.Message, cancellationToken);
        }
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
