namespace dr_mailroom_core.Gmail;

/// <summary>Flusso OAuth di collegamento di una casella: URL di consenso e scambio del code con il refresh token.</summary>
public interface IGmailAuthorizer
{
    /// <summary>URL della pagina di consenso Google (access_type=offline, prompt=consent, scope gmail.readonly).</summary>
    string BuildAuthorizationUrl(string state);

    /// <summary>Scambia il code del callback con il refresh token e legge l'indirizzo della casella autorizzata.</summary>
    Task<ConnectedMailbox> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
}
