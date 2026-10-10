namespace dr_mailroom_core.Gmail;

/// <summary>Google ha rifiutato il refresh token (invalid_grant): la casella va marcata TokenRevoked (T12).</summary>
public sealed class TokenRevokedException(Exception innerException)
    : Exception("Refresh token revocato o scaduto (invalid_grant)", innerException);
