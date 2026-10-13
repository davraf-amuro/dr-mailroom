using dr_mailroom_core.Gmail;
using dr_mailroom_core.Security;

namespace dr_mailroom_tests.Ingestion;

/// <summary>Casella Gmail finta e programmabile: pagine di backfill, history, metadati e guasti iniettabili.</summary>
public sealed class FakeGmailSession : IGmailSession
{
    private readonly Lock _sync = new();
    private int _metadataInFlight;

    /// <summary>Massimo numero di letture di metadati contemporanee osservato.</summary>
    public int MaxConcurrentMetadataReads { get; private set; }

    /// <summary>historyId restituito da getProfile.</summary>
    public ulong ProfileHistoryId { get; set; } = 1000;

    /// <summary>Pagine del backfill/scansione indicizzate per pageToken ("" = prima pagina).</summary>
    public Dictionary<string, MessageIdPage> Pages { get; } = [];

    /// <summary>Risposta di history.list; null = history scaduta (404).</summary>
    public HistoryChanges? Changes { get; set; } = new([], 1000);

    /// <summary>pageToken per cui ListMessageIdsPageAsync deve fallire (simula un'interruzione).</summary>
    public string? FailOnPageToken { get; set; }

    /// <summary>Id per cui GetMetadataAsync deve fallire.</summary>
    public string? FailOnMetadataId { get; set; }

    /// <summary>Eccezione lanciata da ogni lettura di pagine/history/profilo (es. TokenRevokedException).</summary>
    public Exception? FailEverythingWith { get; set; }

    /// <summary>Totale messaggi restituito da getProfile.</summary>
    public int? ProfileMessagesTotal { get; set; } = 1200;

    public List<string?> RequestedPageTokens { get; } = [];

    /// <summary>Se valorizzato, GetMetadataAsync lo completa e attende l'annullamento del giro.</summary>
    public TaskCompletionSource? MetadataReached { get; set; }

    public List<ulong> RequestedHistoryIds { get; } = [];

    /// <summary>Mittente per id messaggio; assente = fornitore@example.com.</summary>
    public Dictionary<string, string> SenderById { get; } = [];

    /// <summary>Label per id messaggio; assente = solo INBOX.</summary>
    public Dictionary<string, string[]> LabelsById { get; } = [];

    public Task<GmailProfile> GetProfileAsync(CancellationToken cancellationToken) =>
        FailEverythingWith is null ? Task.FromResult(new GmailProfile(ProfileHistoryId, ProfileMessagesTotal)) : throw FailEverythingWith;

    public Task<MessageIdPage> ListMessageIdsPageAsync(string? pageToken, CancellationToken cancellationToken)
    {
        RequestedPageTokens.Add(pageToken);
        if (FailEverythingWith is not null)
        {
            throw FailEverythingWith;
        }

        if (pageToken is not null && pageToken == FailOnPageToken)
        {
            throw new HttpRequestException("interruzione simulata");
        }

        return Task.FromResult(Pages[pageToken ?? string.Empty]);
    }

    public Task<HistoryChanges> ListChangesSinceAsync(ulong historyId, CancellationToken cancellationToken)
    {
        RequestedHistoryIds.Add(historyId);
        if (FailEverythingWith is not null)
        {
            throw FailEverythingWith;
        }

        return Changes is null
            ? throw new HistoryExpiredException(historyId, new InvalidOperationException("404"))
            : Task.FromResult(Changes);
    }

    public async Task<GmailMessageMetadata> GetMetadataAsync(string gmailMessageId, CancellationToken cancellationToken)
    {
        var inFlight = Interlocked.Increment(ref _metadataInFlight);
        lock (_sync)
        {
            MaxConcurrentMetadataReads = Math.Max(MaxConcurrentMetadataReads, inFlight);
        }

        try
        {
            // Cedo il controllo: chiamate concorrenti, se ci fossero, si sovrapporrebbero qui
            await Task.Yield();
            if (MetadataReached is not null)
            {
                // Giro "in corso": segnalo l'arrivo e resto qui finché il giro non viene annullato
                MetadataReached.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (gmailMessageId == FailOnMetadataId)
            {
                throw new HttpRequestException("metadati non disponibili");
            }

            return new GmailMessageMetadata(
                gmailMessageId, "t-" + gmailMessageId, SenderById.GetValueOrDefault(gmailMessageId, "fornitore@example.com"), "Fornitore",
                "Oggetto " + gmailMessageId, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
                LabelsById.GetValueOrDefault(gmailMessageId, ["INBOX"]), false);
        }
        finally
        {
            Interlocked.Decrement(ref _metadataInFlight);
        }
    }

    /// <summary>Parti allegato restituite per ogni messaggio.</summary>
    public List<GmailAttachmentPart> AttachmentParts { get; } = [];

    /// <summary>PartId il cui download deve fallire (simula un crash a metà estrazione).</summary>
    public string? FailOnDownloadPartId { get; set; }

    /// <summary>PartId scaricati, nell'ordine.</summary>
    public List<string> DownloadedPartIds { get; } = [];

    public Task<IReadOnlyList<GmailAttachmentPart>> GetAttachmentPartsAsync(string gmailMessageId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GmailAttachmentPart>>(AttachmentParts.ToList());

    public Task<byte[]> DownloadAttachmentAsync(string gmailMessageId, GmailAttachmentPart part, CancellationToken cancellationToken)
    {
        if (part.PartId == FailOnDownloadPartId)
        {
            throw new HttpRequestException("download interrotto");
        }

        // I consumatori della coda scaricano in parallelo
        lock (_sync)
        {
            DownloadedPartIds.Add(part.PartId);
        }

        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes("contenuto " + part.PartId));
    }

    /// <summary>Contenuto raw restituito per ogni messaggio; null = .eml minimale generato dall'id.</summary>
    public byte[]? RawContent { get; set; }

    /// <summary>Id per cui GetRawAsync deve fallire.</summary>
    public string? FailOnRawId { get; set; }

    /// <summary>Id letti in raw, nell'ordine.</summary>
    public List<string> RawRequestedIds { get; } = [];

    public Task<byte[]> GetRawAsync(string gmailMessageId, CancellationToken cancellationToken)
    {
        if (gmailMessageId == FailOnRawId)
        {
            throw new HttpRequestException("raw non disponibile");
        }

        lock (_sync)
        {
            RawRequestedIds.Add(gmailMessageId);
        }

        return Task.FromResult(RawContent ?? System.Text.Encoding.UTF8.GetBytes(
            $"From: fornitore@example.com\r\nSubject: Oggetto {gmailMessageId}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nCorpo {gmailMessageId}\r\n"));
    }

    public void Dispose()
    {
    }
}

/// <summary>Reader che restituisce sempre la stessa sessione finta.</summary>
public sealed class FakeGmailReader(FakeGmailSession session) : IGmailReader
{
    public IGmailSession OpenSession(string refreshToken) => session;
}

/// <summary>Reader con una sessione finta per refresh token: simula più caselle.</summary>
public sealed class FakeMultiMailboxReader : IGmailReader
{
    public Dictionary<string, FakeGmailSession> Sessions { get; } = [];

    public IGmailSession OpenSession(string refreshToken) => Sessions[refreshToken];
}

/// <summary>Protettore identità: i test non verificano la cifratura.</summary>
public sealed class PlainTokenProtector : ITokenProtector
{
    public string Protect(string refreshToken) => refreshToken;

    public string Unprotect(string encryptedRefreshToken) => encryptedRefreshToken;
}
