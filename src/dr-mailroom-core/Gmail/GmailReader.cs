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

/// <summary>Crea sessioni Gmail autenticate con il solo scope gmail.readonly (T1).</summary>
public sealed class GmailReader(
    IOptions<GmailOptions> gmailOptions,
    IOptions<ProcessingOptions> processingOptions,
    GmailRetrySettings retrySettings,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IGmailReader
{
    /// <summary>Nome applicazione inviato a Google negli header delle richieste.</summary>
    public const string ApplicationName = "dr-mailroom";

    /// <summary>Costruisce credenziale e client Gmail; il primo uso rinnova l'access token dal refresh token.</summary>
    public IGmailSession OpenSession(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var gmail = gmailOptions.Value;

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = gmail.ClientId, ClientSecret = gmail.ClientSecret },
            Scopes = [GmailConstants.ReadOnlyScope],
            HttpClientFactory = httpClientFactory,
        });

        var credential = new UserCredential(flow, GmailConstants.Me, new TokenResponse { RefreshToken = refreshToken });

        // Backoff della libreria disattivato: i ritentativi li gestisce GmailSession (T9)
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            HttpClientFactory = httpClientFactory,
            ApplicationName = ApplicationName,
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
        });

        return new GmailSession(
            service,
            flow,
            processingOptions.Value.IncludeSpamTrash,
            retrySettings,
            gmail.QuotaUnitsPerMinute,
            loggerFactory.CreateLogger<GmailSession>());
    }
}
