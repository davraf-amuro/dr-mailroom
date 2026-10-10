namespace dr_mailroom_core.Gmail;

/// <summary>Punto di accesso in sola lettura a Gmail: una sessione per casella, autenticata dal suo refresh token.</summary>
public interface IGmailReader
{
    /// <summary>Apre una sessione per la casella; il token in chiaro non esce dalla sessione e non va loggato.</summary>
    IGmailSession OpenSession(string refreshToken);
}
