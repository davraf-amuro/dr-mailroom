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
    /// <summary>Massimo di messaggi con data 1970-01-01 riletti per giro: 5 unità di quota ciascuno.</summary>
    public const int MaxUndatedRepairsPerRun = 50;

    /// <summary>Esegue l'ingestione della casella per il giro indicato; newMessages null = nessun messaggio da elaborare.</summary>
    public async Task<IngestionResult> IngestAsync(
        int mailboxId, long runId, IGmailSession session, ChannelWriter<long>? newMessages, CancellationToken cancellationToken)
    {
        // Leggo da DB casella e giro con tracking: checkpoint e contatori si salvano insieme ai messaggi
        var mailbox = await mailboxProvider.GetMailboxForUpdateAsync(mailboxId, cancellationToken)
            ?? throw new InvalidOperationException($"Casella {mailboxId} inesistente");
        var run = await runProvider.GetRunForUpdateAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Giro {runId} inesistente");

        await RepairUndatedMessagesAsync(session, mailbox.Id, cancellationToken);

        return mailbox.BackfillCompleted
            ? await IngestIncrementalAsync(session, mailbox, run, newMessages, cancellationToken)
            : await BackfillAsync(session, mailbox, run, cancellationToken);
    }

    /// <summary>
    /// Rilegge da Gmail i messaggi salvati con data 1970-01-01 e ne corregge la data (al massimo MaxUndatedRepairsPerRun per giro).
    /// Un messaggio non più leggibile resta com'è e verrà ritentato al giro successivo.
    /// </summary>
    private async Task RepairUndatedMessagesAsync(IGmailSession session, int mailboxId, CancellationToken cancellationToken)
    {
        var undated = await messageProvider.GetUndatedMessagesAsync(mailboxId, MaxUndatedRepairsPerRun, cancellationToken);
        if (undated.Count == 0)
        {
            return;
        }

        logger.LogInformation("Casella {MailboxId}: {Count} messaggi con data 1970-01-01 da rileggere", mailboxId, undated.Count);
        var repaired = 0;
        foreach (var message in undated)
        {
            GmailMessageMetadata metadata;
            try
            {
                // Chiamo Gmail: metadati del messaggio senza data
                metadata = await session.GetMetadataAsync(message.GmailMessageId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TokenRevokedException)
            {
                logger.LogWarning(ex, "Casella {MailboxId}: messaggio {GmailMessageId} non riletto, data invariata", mailboxId, message.GmailMessageId);
                continue;
            }

            logger.LogInformation(
                "Casella {MailboxId}: data del messaggio {GmailMessageId} corretta in {ReceivedAtUtc} (fonte {Source}, internalDate {InternalDate})",
                mailboxId, message.GmailMessageId, metadata.ReceivedAtUtc, metadata.ReceivedDateSource, metadata.RawInternalDate);
            message.ReceivedAtUtc = metadata.ReceivedAtUtc;
            repaired++;
        }

        // Scrivo su DB le date corrette (YearMonth è una colonna calcolata persistita: si aggiorna da sola)
        await messageProvider.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Casella {MailboxId}: {Repaired} date corrette su {Count}", mailboxId, repaired, undated.Count);
    }

    /// <summary>Backfill: indicizza lo storico pagina per pagina, salvando il pageToken dopo ogni pagina (ripresa).</summary>
    private async Task<IngestionResult> BackfillAsync(IGmailSession session, Mailbox mailbox, Run run, CancellationToken cancellationToken)
    {
        // Chiamo Gmail: profilo letto PRIMA della prima pagina, così l'incrementale copre le mail arrivate durante il backfill;
        // il totale messaggi si aggiorna anche alla ripresa (denominatore dell'avanzamento)
        var profile = await session.GetProfileAsync(cancellationToken);
        mailbox.MessagesTotal = profile.MessagesTotal;

        if (mailbox.HistoryId is null)
        {
            mailbox.HistoryId = profile.HistoryId;
            logger.LogInformation(
                "Casella {MailboxId}: inizio backfill, historyId base {HistoryId}, {MessagesTotal} messaggi in Gmail",
                mailbox.Id, mailbox.HistoryId, mailbox.MessagesTotal);
        }
        else
        {
            logger.LogInformation(
                "Casella {MailboxId}: ripresa backfill dalla pagina salvata, {MessagesTotal} messaggi in Gmail", mailbox.Id, mailbox.MessagesTotal);
        }

        // Scrivo su DB historyId base e totale messaggi
        await mailboxProvider.SaveChangesAsync(cancellationToken);

        var inserted = new List<long>();
        var read = 0;

        do
        {
            // Chiamo Gmail: pagina successiva dello storico
            var page = await session.ListMessageIdsPageAsync(mailbox.BackfillPageToken, cancellationToken);
            var candidates = await FetchMessagesAsync(session, mailbox, page.MessageIds, cancellationToken);

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

        var candidates = await FetchMessagesAsync(session, mailbox, changes.AddedMessageIds, cancellationToken);
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
        var newHistoryId = (await session.GetProfileAsync(cancellationToken)).HistoryId;

        var inserted = new List<long>();
        var read = 0;
        string? pageToken = null;

        do
        {
            // Chiamo Gmail: pagina della scansione completa
            var page = await session.ListMessageIdsPageAsync(pageToken, cancellationToken);
            var candidates = await FetchMessagesAsync(session, mailbox, page.MessageIds, cancellationToken);

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

    /// <summary>
    /// Legge i metadati una mail per volta: niente chiamate concorrenti verso Gmail, la quota è per utente.
    /// Un messaggio eliminato da Gmail dopo l'elenco (404) si salta: non deve bloccare il giro né il checkpoint.
    /// </summary>
    private async Task<IReadOnlyList<Message>> FetchMessagesAsync(
        IGmailSession session, Mailbox mailbox, IReadOnlyList<string> gmailIds, CancellationToken cancellationToken)
    {
        var messages = new List<Message>(gmailIds.Count);
        foreach (var gmailId in gmailIds)
        {
            GmailMessageMetadata metadata;
            try
            {
                // Chiamo Gmail: metadati del messaggio
                metadata = await session.GetMetadataAsync(gmailId, cancellationToken);
            }
            catch (GmailMessageNotFoundException)
            {
                logger.LogWarning("Casella {MailboxId}: messaggio {GmailMessageId} eliminato da Gmail, saltato", mailbox.Id, gmailId);
                continue;
            }

            messages.Add(ToEntity(mailbox, metadata));
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
    private static Message ToEntity(Mailbox mailbox, GmailMessageMetadata metadata) => new()
    {
        MailboxId = mailbox.Id,
        GmailMessageId = metadata.GmailMessageId,
        ThreadId = metadata.ThreadId,
        SenderAddress = TextLimits.Truncate(metadata.SenderAddress, ColumnLengths.EmailAddress)!,
        SenderName = TextLimits.Truncate(metadata.SenderName, ColumnLengths.DisplayName),
        Subject = TextLimits.Truncate(metadata.Subject, ColumnLengths.Subject),
        ReceivedAtUtc = metadata.ReceivedAtUtc,
        Labels = TextLimits.Truncate(string.Join(',', metadata.LabelIds), ColumnLengths.Labels),
        HasAttachments = metadata.HasAttachments,
        IsSent = IsSent(mailbox, metadata),
    };

    /// <summary>Posta inviata: label SENT (copre gli alias send-as) o mittente uguale all'indirizzo della casella. Dalle label complete, prima del troncamento.</summary>
    private static bool IsSent(Mailbox mailbox, GmailMessageMetadata metadata) =>
        metadata.LabelIds.Contains(GmailConstants.SentLabel)
        || string.Equals(metadata.SenderAddress, mailbox.EmailAddress, StringComparison.OrdinalIgnoreCase);
}
