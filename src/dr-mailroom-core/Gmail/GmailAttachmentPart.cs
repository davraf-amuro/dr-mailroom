namespace dr_mailroom_core.Gmail;

/// <summary>Parte MIME di un messaggio che contiene un allegato con nome file.</summary>
public sealed record GmailAttachmentPart(
    string PartId,
    string FileName,
    string? MimeType,
    long SizeBytes,
    string? AttachmentId,
    string? InlineData);
