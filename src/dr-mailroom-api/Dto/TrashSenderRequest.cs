namespace dr_mailroom_api.Dto;

/// <summary>
/// Body di "Elimina tutte": la casella e il mittente le cui mail ricevute vanno nel cestino di Gmail (D-T1 bis).
/// ReceivedBeforeUtc facoltativo, estremo escluso: solo le mail ricevute prima di quell'istante; null = tutte.
/// </summary>
public record TrashSenderRequest(int? MailboxId, string? SenderAddress, DateTime? ReceivedBeforeUtc = null);
