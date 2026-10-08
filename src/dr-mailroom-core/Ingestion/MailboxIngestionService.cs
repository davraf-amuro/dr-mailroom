using System.Threading.Channels;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Ingestion;

/// <summary>
/// Legge una casella e indicizza i messaggi: backfill riprendibile al primo giro (D2, T3), incrementale da historyId poi.
/// Ogni blocco salva messaggi, checkpoint e contatori in un unico SaveChanges: un errore lascia il checkpoint invariato.
/// È il produttore della coda di elaborazione: scarica da Gmail una mail per volta (quota per utente) e,
/// fuori dal backfill (D12), scrive nella coda gli id dei messaggi appena salvati.
/// </summary>
public class MailboxIngestionService(
    MailboxProvider mailboxProvider,
    RunProvider runProvider,
    MessageProvider messageProvider,
    ILogger<MailboxIngestionService> logger)
{
    /// <summary>Esegue l'ingestione della casella per il giro indicato; newMessages null = nessun messaggio da elaborare.</summary>
    public async Task<IngestionResult> IngestAsync(
        int mailboxId, long runId, IGmailSession session, ChannelWriter<long>? newMessages, CancellationToken cancellationToken)
    {
        // Leggo da DB casella e giro con tracking: checkpoint e contatori si salvano insieme ai messaggi
        var mailbox = await mailboxProvider.GetMailboxForUpdateAsync(mailboxId, cancellationToken)
            ?? throw new InvalidOperationException($"Casella {mailboxId} inesistente");
        var run = await runProvider.GetRunForUpdateAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Giro {runId} inesistente");

        return mailbox.BackfillCompleted
            ? await IngestIncrementalAsync(session, mailbox, run, newMessages, cancellationToken)
            : await BackfillAsync(session, mailbox, run, cancellationToken);
    }

    /// <summary>Backfill: indicizza lo storico pagina per pagina, salvando il pageToken dopo ogni pagina (ripresa).</summary>
    private async Task<IngestionResult> BackfillAsync(IGmailSession session, Mailbox mailbox, Run run, CancellationToken cancellationToken)
    {
        if (mailbox.HistoryId is null)
        {
            // Chiamo Gmail: historyId catturato PRIMA della prima pagina, così l'incrementale copre le mail arrivate durante il backfill
            mailbox.HistoryId = await session.GetProfileHistoryIdAsync(cancellationToken);
            await mailboxProvider.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Casella {MailboxId}: inizio backfill, historyId base {HistoryId}", mailbox.Id, mailbox.HistoryId);
        }
        else
        {
            logger.LogInformation("Casella {MailboxId}: ripresa backfill dalla pagina salvata", mailbox.Id);
        }

        var inserted = new List<long>();
        var read = 0;

        do
        {
            // Chiamo Gmail: pagina successiva dello storico
            var page = await session.ListMessageIdsPageAsync(mailbox.BackfillPageToken, cancellationToken);
            var candidates = await FetchMessagesAsync(session, mailbox.Id, page.MessageIds, cancellationToken);

            read += page.MessageIds.Count;
            run.MessagesRead += page.MessageIds.Count;
            mailbox.BackfillPageToken = page.NextPageToken;
            if (page.NextPageToken is null)
            {
                mailbox.BackfillCompleted = true;
            }

            // Scrivo su DB pagina + pageToken + contatori in un unico SaveChanges
            var saved = await messageProvider.InsertNewMessagesAsync(mailbox.Id, candidates, cancellationToken);
            inserted.AddRange(saved.Select(m => m.Id));
        }
        while (!mailbox.BackfillCompleted);

        logger.LogInformation("Casella {MailboxId}: backfill completato, {Read} messaggi letti, {Inserted} indicizzati", mailbox.Id, read, inserted.Count);
        return new IngestionResult(true, inserted, read);
    }

    /// <summary>Incrementale da HistoryId; history scaduta (404) → scansione completa idempotente (T3).</summary>
    private async Task<IngestionResult> IngestIncrementalAsync(
        IGmailSession session, Mailbox mailbox, Run run, ChannelWriter<long>? newMessages, CancellationToken cancellationToken)
    {
        var baseHistoryId = mailbox.HistoryId
            ?? throw new InvalidOperationException($"Casella {mailbox.Id}: backfill completato ma HistoryId assente");

        HistoryChanges changes;
        try
        {
            // Chiamo Gmail: modifiche dopo l'ultimo checkpoint
            changes = await session.ListChangesSinceAsync(baseHistoryId, cancellationToken);
        }
        catch (HistoryExpiredException)
        {
            logger.LogWarning("Casella {MailboxId}: history scaduta per historyId {HistoryId}, scansione completa", mailbox.Id, baseHistoryId);
            return await FullScanAsync(session, mailbox, run, newMessages, cancellationToken);
        }

        var candidates = await FetchMessagesAsync(session, mailbox.Id, changes.AddedMessageIds, cancellationToken);
        run.MessagesRead += changes.AddedMessageIds.Count;
        mailbox.HistoryId = changes.LatestHistoryId;

        // Scrivo su DB messaggi + nuovo HistoryId + contatori: se qualcosa fallisce prima, il checkpoint resta quello vecchio
        var saved = await messageProvider.InsertNewMessagesAsync(mailbox.Id, candidates, cancellationToken);
        await EnqueueAsync(newMessages, saved, cancellationToken);

        logger.LogInformation(
            "Casella {MailboxId}: incrementale da {FromHistoryId} a {ToHistoryId}, {Inserted} messaggi nuovi",
            mailbox.Id, baseHistoryId, changes.LatestHistoryId, saved.Count);
        return new IngestionResult(false, saved.Select(m => m.Id).ToList(), changes.AddedMessageIds.Count);
    }

    /// <summary>Rilegge tutta la casella; l'insert idempotente fa entrare solo i messaggi mancanti, che poi verranno filtrati.</summary>
    private async Task<IngestionResult> FullScanAsync(
        IGmailSession session, Mailbox mailbox, Run run, ChannelWriter<long>? newMessages, CancellationToken cancellationToken)
    {
        // Chiamo Gmail: nuovo historyId catturato prima della scansione
        var newHistoryId = await session.GetProfileHistoryIdAsync(cancellationToken);

        var inserted = new List<long>();
        var read = 0;
        string? pageToken = null;

        do
        {
            // Chiamo Gmail: pagina della scansione completa
            var page = await session.ListMessageIdsPageAsync(pageToken, cancellationToken);
            var candidates = await FetchMessagesAsync(session, mailbox.Id, page.MessageIds, cancellationToken);

            read += page.MessageIds.Count;
            run.MessagesRead += page.MessageIds.Count;
            pageToken = page.NextPageToken;
            if (pageToken is null)
            {
                mailbox.HistoryId = newHistoryId;
            }

            // Scrivo su DB la pagina; il nuovo HistoryId si salva solo con l'ultima pagina
            var saved = await messageProvider.InsertNewMessagesAsync(mailbox.Id, candidates, cancellationToken);
            inserted.AddRange(saved.Select(m => m.Id));
            await EnqueueAsync(newMessages, saved, cancellationToken);
        }
        while (pageToken is not null);

        logger.LogInformation("Casella {MailboxId}: scansione completa, {Read} letti, {Inserted} nuovi", mailbox.Id, read, inserted.Count);
        return new IngestionResult(false, inserted, read);
    }

    /// <summary>Legge i metadati una mail per volta: niente chiamate concorrenti verso Gmail, la quota è per utente.</summary>
    private static async Task<IReadOnlyList<Message>> FetchMessagesAsync(
        IGmailSession session, int mailboxId, IReadOnlyList<string> gmailIds, CancellationToken cancellationToken)
    {
        var messages = new List<Message>(gmailIds.Count);
        foreach (var gmailId in gmailIds)
        {
            // Chiamo Gmail: metadati del messaggio
            var metadata = await session.GetMetadataAsync(gmailId, cancellationToken);
            messages.Add(ToEntity(mailboxId, metadata));
        }

        return messages;
    }

    /// <summary>Accoda in ordine (FIFO) gli id dei messaggi appena salvati; attende se la coda è piena.</summary>
    private static async Task EnqueueAsync(ChannelWriter<long>? newMessages, IEnumerable<Message> saved, CancellationToken cancellationToken)
    {
        if (newMessages is null)
        {
            return;
        }

        foreach (var message in saved)
        {
            await newMessages.WriteAsync(message.Id, cancellationToken);
        }
    }

    /// <summary>Converte i metadati Gmail nell'entità, adattando i testi alle lunghezze delle colonne.</summary>
    private static Message ToEntity(int mailboxId, GmailMessageMetadata metadata) => new()
    {
        MailboxId = mailboxId,
        GmailMessageId = metadata.GmailMessageId,
        ThreadId = metadata.ThreadId,
        SenderAddress = TextLimits.Truncate(metadata.SenderAddress, ColumnLengths.EmailAddress)!,
        SenderName = TextLimits.Truncate(metadata.SenderName, ColumnLengths.DisplayName),
        Subject = TextLimits.Truncate(metadata.Subject, ColumnLengths.Subject),
        ReceivedAtUtc = metadata.ReceivedAtUtc,
        Labels = TextLimits.Truncate(string.Join(',', metadata.LabelIds), ColumnLengths.Labels),
        HasAttachments = metadata.HasAttachments,
    };
}
