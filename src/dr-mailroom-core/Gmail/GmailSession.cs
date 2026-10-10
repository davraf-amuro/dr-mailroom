using System.Net;
using System.Threading.RateLimiting;
using Google;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
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
    /// <summary>Gettoni = unità di quota, riforniti ogni secondo senza burst; le chiamate in eccesso attendono in coda.</summary>
    private readonly TokenBucketRateLimiter _quotaLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = quotaUnitsPerMinute / 60,
        TokensPerPeriod = quotaUnitsPerMinute / 60,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = int.MaxValue,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });

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
        _quotaLimiter.Dispose();
        service.Dispose();
        flow.Dispose();
    }

    /// <summary>Esegue una chiamata Gmail: attende le unità di quota; invalid_grant → TokenRevokedException; quota superata e 5xx → ritenta con backoff esponenziale.</summary>
    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> call, int cost, string operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Ogni tentativo consuma quota: attendo i gettoni prima di chiamare
            using var lease = await _quotaLimiter.AcquireAsync(cost, cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new InvalidOperationException($"Gmail {operation}: quota locale non acquisita");
            }

            try
            {
                return await call(cancellationToken);
            }
            catch (TokenResponseException ex) when (ex.Error?.Error == GmailConstants.InvalidGrant)
            {
                throw new TokenRevokedException(ex);
            }
            catch (GoogleApiException ex) when (IsRateLimited(ex) && attempt < retry.MaxAttempts)
            {
                var delay = retry.RateLimitBaseDelay * Math.Pow(2, attempt - 1);
                // Reason e messaggio di Google indicano quale quota è scattata (utente, progetto, al minuto o al secondo)
                var reasons = string.Join(',', (ex.Error?.Errors ?? []).Select(e => e.Reason));
                logger.LogWarning(
                    "Gmail {Operation}: quota superata (HTTP {StatusCode}, reason {Reasons}: {GoogleMessage}), tentativo {Attempt} di {MaxAttempts}, nuovo tentativo tra {DelayMs} ms",
                    operation, (int)ex.HttpStatusCode, reasons, ex.Error?.Message, attempt, retry.MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (GoogleApiException ex) when ((int)ex.HttpStatusCode >= 500 && attempt < retry.MaxAttempts)
            {
                var delay = retry.BaseDelay * Math.Pow(2, attempt - 1);
                logger.LogWarning(
                    "Gmail {Operation}: HTTP {StatusCode}, tentativo {Attempt} di {MaxAttempts}, nuovo tentativo tra {DelayMs} ms",
                    operation, (int)ex.HttpStatusCode, attempt, retry.MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>Quota superata: 429, oppure 403 con reason rateLimitExceeded/userRateLimitExceeded; gli altri 403 non migliorano ritentando.</summary>
    private static bool IsRateLimited(GoogleApiException ex) =>
        ex.HttpStatusCode == HttpStatusCode.TooManyRequests
        || (ex.HttpStatusCode == HttpStatusCode.Forbidden
            && (ex.Error?.Errors ?? []).Any(e => e.Reason is GmailConstants.RateLimitExceededReason or GmailConstants.UserRateLimitExceededReason));

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
