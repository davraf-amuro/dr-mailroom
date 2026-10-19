namespace dr_mailroom_core.Gmail;

/// <summary>Gmail non trova più il messaggio (HTTP 404): eliminato definitivamente dopo essere comparso in history o nella lista.</summary>
public sealed class GmailMessageNotFoundException(string gmailMessageId, Exception innerException)
    : Exception($"Messaggio Gmail {gmailMessageId} non trovato (eliminato)", innerException)
{
    public string GmailMessageId { get; } = gmailMessageId;
}
