namespace dr_mailroom_api.Services;

/// <summary>
/// File da restituire per il download del contenuto: FilePath per il .eml servito in streaming dal disco,
/// Content per il testo ricavato al volo. Uno solo dei due è valorizzato.
/// </summary>
public sealed record MessageContentFile(string? FilePath, byte[]? Content, string FileName, string ContentType);
