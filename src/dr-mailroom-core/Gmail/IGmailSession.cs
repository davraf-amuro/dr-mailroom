namespace dr_mailroom_core.Gmail;

/// <summary>Operazioni di lettura su una casella. Nessun metodo modifica Gmail (T1).</summary>
public interface IGmailSession : IDisposable
{
    /// <summary>Profilo della casella: historyId corrente (base dell'incrementale, T3) e totale messaggi (avanzamento del backfill).</summary>
    Task<GmailProfile> GetProfileAsync(CancellationToken cancellationToken);

    /// <summary>Una pagina di id messaggio per il backfill; pageToken null = prima pagina.</summary>
    Task<MessageIdPage> ListMessageIdsPageAsync(string? pageToken, CancellationToken cancellationToken);

    /// <summary>Messaggi aggiunti dopo historyId (solo messageAdded). Lancia HistoryExpiredException su HTTP 404.</summary>
    Task<HistoryChanges> ListChangesSinceAsync(ulong historyId, CancellationToken cancellationToken);

    /// <summary>Metadati di un messaggio (format=metadata): mittente, oggetto, data, label, presenza allegati. Lancia GmailMessageNotFoundException su HTTP 404.</summary>
    Task<GmailMessageMetadata> GetMetadataAsync(string gmailMessageId, CancellationToken cancellationToken);

    /// <summary>Parti allegato del messaggio (format=full), senza scaricarne il contenuto.</summary>
    Task<IReadOnlyList<GmailAttachmentPart>> GetAttachmentPartsAsync(string gmailMessageId, CancellationToken cancellationToken);

    /// <summary>Contenuto binario di un allegato.</summary>
    Task<byte[]> DownloadAttachmentAsync(string gmailMessageId, GmailAttachmentPart part, CancellationToken cancellationToken);

    /// <summary>Messaggio originale RFC 822 (format=raw), salvato come .eml dal processo ContentArchive.</summary>
    Task<byte[]> GetRawAsync(string gmailMessageId, CancellationToken cancellationToken);
}
