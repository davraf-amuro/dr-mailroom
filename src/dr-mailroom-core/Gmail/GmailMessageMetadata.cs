namespace dr_mailroom_core.Gmail;

/// <summary>
/// Metadati di un messaggio letti con format=metadata: quanto serve a statistiche e filtri.
/// ReceivedDateSource e RawInternalDate dicono da dove viene la data (diagnostica delle date assenti).
/// </summary>
public sealed record GmailMessageMetadata(
    string GmailMessageId,
    string ThreadId,
    string SenderAddress,
    string? SenderName,
    string? Subject,
    DateTime ReceivedAtUtc,
    IReadOnlyList<string> LabelIds,
    bool HasAttachments,
    ReceivedDateSource ReceivedDateSource = ReceivedDateSource.InternalDate,
    long? RawInternalDate = null);
