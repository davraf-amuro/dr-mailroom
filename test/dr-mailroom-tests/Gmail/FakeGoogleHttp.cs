using System.Net;
using System.Text;
using Google.Apis.Http;

namespace dr_mailroom_tests.Gmail;

/// <summary>Handler HTTP finto: risponde al token endpoint OAuth e instrada le chiamate Gmail su risposte programmate.</summary>
public sealed class FakeGoogleHandler(Func<HttpRequestMessage, int, HttpResponseMessage> gmailResponder) : HttpMessageHandler
{
    private const string TokenOk = """{"access_token":"fake-access","expires_in":3600,"token_type":"Bearer"}""";

    /// <summary>Risposta del token endpoint; sostituibile per simulare invalid_grant.</summary>
    public Func<HttpResponseMessage> TokenResponder { get; set; } = () => Json(HttpStatusCode.OK, TokenOk);

    /// <summary>Numero di chiamate Gmail ricevute (token endpoint escluso).</summary>
    public int GmailCalls { get; private set; }

    /// <summary>Url delle chiamate Gmail, nell'ordine.</summary>
    public List<string> GmailUrls { get; } = [];

    /// <summary>Instrada la richiesta: token endpoint oppure API Gmail.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        if (url.Contains("/token", StringComparison.Ordinal))
        {
            return Task.FromResult(TokenResponder());
        }

        GmailCalls++;
        GmailUrls.Add(url);
        return Task.FromResult(gmailResponder(request, GmailCalls));
    }

    /// <summary>Risposta JSON con lo status indicato.</summary>
    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Errore nel formato delle API Google.</summary>
    public static HttpResponseMessage GoogleError(HttpStatusCode status) =>
        Json(status, $$$"""{"error":{"code":{{{(int)status}}},"message":"fake {{{status}}}","errors":[]}}""");

    /// <summary>Errore nel formato delle API Google con una reason (es. rateLimitExceeded).</summary>
    public static HttpResponseMessage GoogleError(HttpStatusCode status, string reason) =>
        Json(status, $$$"""{"error":{"code":{{{(int)status}}},"message":"fake {{{reason}}}","errors":[{"domain":"usageLimits","reason":"{{{reason}}}","message":"fake"}]}}""");
}

/// <summary>Factory HTTP della libreria Google che usa l'handler finto.</summary>
public sealed class FakeGoogleHttpClientFactory(HttpMessageHandler handler) : HttpClientFactory
{
    /// <summary>Restituisce l'handler finto al posto di quello di rete.</summary>
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => handler;
}
