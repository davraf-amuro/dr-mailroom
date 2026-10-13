using dr_mailroom_core.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Gmail;

/// <summary>Crea sessioni del cestino autenticate con lo scope gmail.modify del token separato (D-T1 bis).</summary>
public sealed class GmailTrasher(
    IOptions<GmailOptions> gmailOptions,
    GmailRetrySettings retrySettings,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IGmailTrasher
{
    /// <summary>Costruisce credenziale e client Gmail del cestino; il primo uso rinnova l'access token dal refresh token.</summary>
    public IGmailTrashSession OpenSession(string trashRefreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trashRefreshToken);
        var gmail = gmailOptions.Value;

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = gmail.ClientId, ClientSecret = gmail.ClientSecret },
            Scopes = [GmailConstants.ModifyScope],
            HttpClientFactory = httpClientFactory,
        });

        var credential = new UserCredential(flow, GmailConstants.Me, new TokenResponse { RefreshToken = trashRefreshToken });

        // Backoff della libreria disattivato: i ritentativi li gestisce GmailCallExecutor (T9)
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            HttpClientFactory = httpClientFactory,
            ApplicationName = GmailReader.ApplicationName,
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
        });

        return new GmailTrashSession(
            service, flow, retrySettings, gmail.QuotaUnitsPerMinute, loggerFactory.CreateLogger<GmailTrashSession>());
    }
}
