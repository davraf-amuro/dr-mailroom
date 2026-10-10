namespace dr_mailroom_core.Gmail;

/// <summary>Esito del consenso OAuth: indirizzo della casella e refresh token in chiaro (da cifrare subito, mai loggare).</summary>
public sealed record ConnectedMailbox(string EmailAddress, string RefreshToken);
