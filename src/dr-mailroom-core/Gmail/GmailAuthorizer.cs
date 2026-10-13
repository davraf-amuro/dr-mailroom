using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Gmail;

/// <summary>Flusso OAuth per applicazione web: scope gmail.readonly per collegare (T1, C2), gmail.modify per il cestino (D-T1 bis).</summary>
public sealed class GmailAuthorizer(
    IOptions<GmailOptions> gmailOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<GmailAuthorizer> logger) : IGmailAuthorizer
{
    /// <summary>access_type=offline: Google restituisce il refresh token.</summary>
    public const string OfflineAccess = "offline";

    /// <summary>prompt=consent: il refresh token arriva anche se l'utente aveva già autorizzato l'app.</summary>
    public const string ConsentPrompt = "consent";

    /// <summary>Costruisce l'URL di consenso con lo state firmato contro CSRF.</summary>
    public string BuildAuthorizationUrl(string state, OAuthPurpose purpose)
    {
        var gmail = gmailOptions.Value;
        var request = new GoogleAuthorizationCodeRequestUrl(new Uri(GoogleAuthConsts.AuthorizationUrl))
        {
            ClientId = gmail.ClientId,
            RedirectUri = gmail.RedirectUri,
            Scope = ScopeFor(purpose),
            AccessType = OfflineAccess,
            Prompt = ConsentPrompt,
            State = state,
        };
        return request.Build().AbsoluteUri;
    }

    /// <summary>Scambia il code con i token, poi legge l'indirizzo con users.getProfile.</summary>
    public async Task<ConnectedMailbox> ExchangeCodeAsync(string code, OAuthPurpose purpose, CancellationToken cancellationToken)
    {
        var gmail = gmailOptions.Value;
        using var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = gmail.ClientId, ClientSecret = gmail.ClientSecret },
            Scopes = [ScopeFor(purpose)],
            HttpClientFactory = httpClientFactory,
        });

        // Chiamo Google: scambio del code con access e refresh token
        var token = await flow.ExchangeCodeForTokenAsync(GmailConstants.Me, code, gmail.RedirectUri, cancellationToken);
        if (string.IsNullOrEmpty(token.RefreshToken))
        {
            throw new InvalidOperationException("Google non ha restituito il refresh token: revoca l'accesso dell'app e ricollega la casella");
        }

        using var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = new UserCredential(flow, GmailConstants.Me, token),
            HttpClientFactory = httpClientFactory,
            ApplicationName = GmailReader.ApplicationName,
        });

        // Chiamo Gmail: indirizzo della casella appena autorizzata
        var profile = await service.Users.GetProfile(GmailConstants.Me).ExecuteAsync(cancellationToken);
        var emailAddress = profile.EmailAddress ?? throw new InvalidOperationException("Profilo Gmail senza indirizzo");

        logger.LogInformation("Consenso OAuth {Purpose} ottenuto per una casella Gmail", purpose);
        return new ConnectedMailbox(emailAddress, token.RefreshToken);
    }

    /// <summary>Unico punto in cui lo scopo diventa scope: la lettura non chiede mai gmail.modify.</summary>
    private static string ScopeFor(OAuthPurpose purpose) => purpose switch
    {
        OAuthPurpose.Connect => GmailConstants.ReadOnlyScope,
        OAuthPurpose.Trash => GmailConstants.ModifyScope,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Scopo OAuth non gestito"),
    };
}
