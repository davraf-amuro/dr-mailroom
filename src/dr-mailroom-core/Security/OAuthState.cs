using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Security;

/// <summary>
/// Contenuto dello state: scopo e, per il cestino, la casella da autorizzare e il mittente da cui l'utente è partito
/// (serve solo a riportarlo alla stessa vista). Nonce casuale: due state non sono mai uguali.
/// </summary>
public sealed record OAuthState(OAuthPurpose Purpose, int? MailboxId, string? SenderAddress, string Nonce);
