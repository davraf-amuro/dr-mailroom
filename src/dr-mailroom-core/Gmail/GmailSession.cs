using System.Net;
using Google;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Util;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Gmail;

/// <summary>Sessione Gmail in sola lettura per una casella: quota al minuto rispettata, retry su quota superata e 5xx, mappatura degli errori di dominio.</summary>
public sealed class GmailSession(
    GmailService service,
    IAuthorizationCodeFlow flow,
    bool includeSpamTrash,
    GmailRetrySettings retry,
    int quotaUnitsPerMinute,
    ILogger<GmailSession> logger) : IGmailSession
{
    private readonly GmailCallExecutor _executor = new(retry, quotaUnitsPerMinute, logger);

    /// <summary>Legge historyId corrente e totale messaggi del profilo.</summary>
    public async Task<GmailProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        // Chiamo Gmail: users.getProfile
        var profile = await ExecuteAsync(
            ct => service.Users.GetProfile(GmailConstants.Me).ExecuteAsync(ct), GmailConstants.GetProfileCost, nameof(GetProfileAsync), cancellationToken);
        var historyId = profile.HistoryId ?? throw new InvalidOperationException("Profilo Gmail senza historyId");
        return new GmailProfile(historyId, profile.MessagesTotal);
    }

    /// <summary>Legge una pagina di id messaggio, escludendo Spam e Cestino salvo configurazione (D6).</summary>
    public async Task<MessageIdPage> ListMessageIdsPageAsync(string? pageToken, CancellationToken cancellationToken)
    {
        var request = service.Users.Messages.List(GmailConstants.Me);
        request.PageToken = pageToken;
        request.IncludeSpamTrash = includeSpamTrash;
        request.MaxResults = GmailConstants.MaxPageSize;

        // Chiamo Gmail: users.messages.list
        var response = await ExecuteAsync(request.ExecuteAsync, GmailConstants.MessagesListCost, nameof(ListMessageIdsPageAsync), cancellationToken);
        var ids = (response.Messages ?? []).Where(m => m.Id is not null).Select(m => m.Id).ToList();
        return new MessageIdPage(ids, response.NextPageToken);
    }

    /// <summary>Raccoglie, su tutte le pagine, gli id dei messaggi aggiunti dopo historyId.</summary>
    public async Task<HistoryChanges> ListChangesSinceAsync(ulong historyId, CancellationToken cancellationToken)
    {
        var added = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var latestHistoryId = historyId;
        string? pageToken = null;

        try
        {
            do
            {
                var request = service.Users.History.List(GmailConstants.Me);
                request.StartHistoryId = historyId;
                request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;
                request.PageToken = pageToken;

                // Chiamo Gmail: users.history.list
                var response = await ExecuteAsync(request.ExecuteAsync, GmailConstants.HistoryListCost, nameof(ListChangesSinceAsync), cancellationToken);

                foreach (var message in (response.History ?? []).SelectMany(h => h.MessagesAdded ?? []).Select(a => a.Message))
                {
                    if (message?.Id is null || (!includeSpamTrash && IsSpamOrTrash(message.LabelIds)))
                    {
                        continue;
                    }

                    if (seen.Add(message.Id))
                    {
                        added.Add(message.Id);
                    }
                }

                if (response.HistoryId is ulong responseHistoryId && responseHistoryId > latestHistoryId)
                {
                    latestHistoryId = responseHistoryId;
                }

                pageToken = response.NextPageToken;
            }
            while (pageToken is not null);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            throw new HistoryExpiredException(historyId, ex);
        }

        return new HistoryChanges(added, latestHistoryId);
    }

    /// <summary>Legge i metadati di un messaggio con i soli header From, Subject e Date (Date: ripiego se manca internalDate).</summary>
    public async Task<GmailMessageMetadata> GetMetadataAsync(string gmailMessageId, CancellationToken cancellationToken)
    {
        var request = service.Users.Messages.Get(GmailConstants.Me, gmailMessageId);
        request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
        request.MetadataHeaders = new Repeatable<string>([GmailConstants.FromHeader, GmailConstants.SubjectHeader, GmailConstants.DateHeader]);

        // Chiamo Gmail: users.messages.get (format=metadata)
        var message = await ExecuteAsync(request.ExecuteAsync, GmailConstants.MessagesGetCost, nameof(GetMetadataAsync), cancellationToken);

        var headers = message.Payload?.Headers ?? [];
        var (address, displayName) = SenderParser.Parse(FindHeader(headers, GmailConstants.FromHeader));
        var dateHeader = FindHeader(headers, GmailConstants.DateHeader);
        var received = ReceivedDateResolver.Resolve(message.InternalDate, dateHeader, DateTime.UtcNow);
        if (received.Source != ReceivedDateSource.InternalDate)
        {
            logger.LogWarning(
                "Messaggio {GmailMessageId}: internalDate {InternalDate} non valido, data presa da {Source} (header Date {DateHeader})",
                gmailMessageId, message.InternalDate, received.Source, dateHeader);
        }

        return new GmailMessageMetadata(
            message.Id ?? gmailMessageId,
            message.ThreadId ?? string.Empty,
            address,
            displayName,
            FindHeader(headers, GmailConstants.SubjectHeader),
            received.ReceivedAtUtc,
            message.LabelIds?.ToList() ?? [],
            string.Equals(message.Payload?.MimeType, GmailConstants.MultipartMixed, StringComparison.OrdinalIgnoreCase),
            received.Source,
            message.InternalDate);
    }

    /// <summary>Elenca le parti con nome file del messaggio (format=full), visitando le parti annidate.</summary>
    public async Task<IReadOnlyList<GmailAttachmentPart>> GetAttachmentPartsAsync(string gmailMessageId, CancellationToken cancellationToken)
    {
        var request = service.Users.Messages.Get(GmailConstants.Me, gmailMessageId);
        request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

        // Chiamo Gmail: users.messages.get (format=full)
        var message = await ExecuteAsync(request.ExecuteAsync, GmailConstants.MessagesGetCost, nameof(GetAttachmentPartsAsync), cancellationToken);

        var parts = new List<GmailAttachmentPart>();
        CollectAttachmentParts(message.Payload, parts);
        return parts;
    }

    /// <summary>Scarica il contenuto di un allegato: dati inline se presenti, altrimenti users.messages.attachments.get.</summary>
    public async Task<byte[]> DownloadAttachmentAsync(string gmailMessageId, GmailAttachmentPart part, CancellationToken cancellationToken)
    {
        if (part.InlineData is not null)
        {
            return DecodeBase64Url(part.InlineData);
        }

        var attachmentId = part.AttachmentId
            ?? throw new InvalidOperationException($"Parte {part.PartId} senza attachmentId né dati inline");

        // Chiamo Gmail: users.messages.attachments.get
        var body = await ExecuteAsync(
            ct => service.Users.Messages.Attachments.Get(GmailConstants.Me, gmailMessageId, attachmentId).ExecuteAsync(ct),
            GmailConstants.AttachmentsGetCost,
            nameof(DownloadAttachmentAsync),
            cancellationToken);

        return DecodeBase64Url(body.Data ?? string.Empty);
    }

    /// <summary>Rilascia il limitatore di quota, il client HTTP di Gmail e quello del flusso OAuth.</summary>
    public void Dispose()
    {
        _executor.Dispose();
        service.Dispose();
        flow.Dispose();
    }

    /// <summary>Esegue una chiamata Gmail con quota, ritentativi e mappatura di invalid_grant (GmailCallExecutor).</summary>
    private Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> call, int cost, string operation, CancellationToken cancellationToken) =>
        _executor.ExecuteAsync(call, cost, operation, cancellationToken);

    /// <summary>True se il messaggio è in Spam o nel Cestino.</summary>
    private static bool IsSpamOrTrash(IList<string>? labelIds) =>
        labelIds is not null && (labelIds.Contains(GmailConstants.SpamLabel) || labelIds.Contains(GmailConstants.TrashLabel));

    /// <summary>Valore del primo header con il nome dato (case-insensitive), null se assente.</summary>
    private static string? FindHeader(IEnumerable<MessagePartHeader> headers, string name) =>
        headers.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>Visita ricorsivamente l'albero MIME e raccoglie le parti con nome file.</summary>
    private static void CollectAttachmentParts(MessagePart? part, List<GmailAttachmentPart> parts)
    {
        if (part is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(part.Filename))
        {
            parts.Add(new GmailAttachmentPart(
                part.PartId ?? string.Empty,
                part.Filename,
                part.MimeType,
                part.Body?.Size ?? 0,
                part.Body?.AttachmentId,
                part.Body?.AttachmentId is null ? part.Body?.Data : null));
        }

        foreach (var child in part.Parts ?? [])
        {
            CollectAttachmentParts(child, parts);
        }
    }

    /// <summary>Decodifica il base64url di Gmail (alfabeto -_ e padding facoltativo).</summary>
    private static byte[] DecodeBase64Url(string data)
    {
        var base64 = data.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
