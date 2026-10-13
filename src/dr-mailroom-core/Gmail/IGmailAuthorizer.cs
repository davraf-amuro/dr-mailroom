using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Gmail;

/// <summary>
/// Flusso OAuth di una casella: URL di consenso e scambio del code con il refresh token.
/// Lo scopo decide lo scope: Connect → gmail.readonly (T1), Trash → gmail.modify (D-T1 bis).
/// </summary>
public interface IGmailAuthorizer
{
    /// <summary>URL della pagina di consenso Google (access_type=offline, prompt=consent) con lo scope dello scopo.</summary>
    string BuildAuthorizationUrl(string state, OAuthPurpose purpose);

    /// <summary>Scambia il code del callback con il refresh token e legge l'indirizzo della casella autorizzata.</summary>
    Task<ConnectedMailbox> ExchangeCodeAsync(string code, OAuthPurpose purpose, CancellationToken cancellationToken);
}
