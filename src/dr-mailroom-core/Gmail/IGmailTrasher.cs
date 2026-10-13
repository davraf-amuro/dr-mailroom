namespace dr_mailroom_core.Gmail;

/// <summary>
/// Punto di accesso al cestino di Gmail (D-T1 bis): sessioni autenticate solo dal token gmail.modify della casella,
/// separate da quelle di lettura.
/// </summary>
public interface IGmailTrasher
{
    /// <summary>Apre una sessione per il cestino; il token in chiaro non esce dalla sessione e non va loggato.</summary>
    IGmailTrashSession OpenSession(string trashRefreshToken);
}
