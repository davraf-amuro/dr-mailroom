using Microsoft.AspNetCore.Hosting;

namespace dr_mailroom_tests.Api;

/// <summary>Policy CORS: BaseUrl sempre ammessa, ExtraCorsOrigins solo se configurate, ogni altra origine rifiutata.</summary>
public sealed class CorsTests(MailroomApiFactory factory) : IClassFixture<MailroomApiFactory>
{
    private const string MailboxesUrl = "/api/v1/mailboxes";
    private const string BaseUrl = "http://mailroom.test";
    private const string DevOrigin = "http://localhost:5173";
    private const string ForeignOrigin = "http://evil.test";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    /// <summary>Preflight di una GET con chiave API dall'origine indicata; restituisce l'origine ammessa o null.</summary>
    private static async Task<string?> PreflightAsync(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, MailboxesUrl);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", MailroomApiFactory.ApiKeyHeader);

        using var response = await client.SendAsync(request);
        return response.Headers.TryGetValues(AllowOriginHeader, out var values) ? values.Single() : null;
    }

    private HttpClient CreateClient(params string[] extraOrigins) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mailroom:Frontend:BaseUrl", BaseUrl);
            for (var i = 0; i < extraOrigins.Length; i++)
            {
                builder.UseSetting($"Mailroom:Frontend:ExtraCorsOrigins:{i}", extraOrigins[i]);
            }
        }).CreateClient();

    [Fact]
    public async Task WithoutExtraOrigins_OnlyBaseUrlIsAllowedAsync()
    {
        var client = CreateClient();

        Assert.Equal(BaseUrl, await PreflightAsync(client, BaseUrl));
        Assert.Null(await PreflightAsync(client, DevOrigin));
    }

    [Fact]
    public async Task ExtraOrigin_IsAllowed_AlongsideBaseUrlAsync()
    {
        var client = CreateClient(DevOrigin + "/");

        Assert.Equal(BaseUrl, await PreflightAsync(client, BaseUrl));
        Assert.Equal(DevOrigin, await PreflightAsync(client, DevOrigin));
    }

    [Fact]
    public async Task ForeignOrigin_IsRejectedAsync()
    {
        var client = CreateClient(DevOrigin);

        Assert.Null(await PreflightAsync(client, ForeignOrigin));
    }
}
