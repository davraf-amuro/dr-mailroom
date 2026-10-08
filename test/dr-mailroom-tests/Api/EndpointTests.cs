using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;

namespace dr_mailroom_tests.Api;

/// <summary>Test di integrazione degli endpoint: autenticazione, validazione dei body (D15), 409 su casella occupata o eliminata.</summary>
public sealed class EndpointTests : IClassFixture<MailroomApiFactory>
{
    private const string FiltersUrl = "/api/v1/filters";
    private const string RunsUrl = "/api/v1/runs";

    private readonly MailroomApiFactory _factory;
    private readonly HttpClient _client;

    public EndpointTests(MailroomApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthorizedClient();
    }

    /// <summary>Body di filtro valido, modificabile campo per campo.</summary>
    private static Dictionary<string, object?> ValidFilter() => new()
    {
        ["name"] = "Fatture fornitore",
        ["senderAddress"] = "fatture@fornitore.it",
        ["subjectMatchType"] = "Contains",
        ["subjectValue"] = "fattura",
        ["attachmentSubfolder"] = "fornitore",
        ["processes"] = new[] { "AttachmentExtraction", "Alert" },
    };

    /// <summary>Crea una casella direttamente nel DB InMemory dell'host.</summary>
    private async Task<int> SeedMailboxAsync(MailboxStatus status = MailboxStatus.Active)
    {
        await using var db = _factory.NewContext();
        var mailbox = new Mailbox
        {
            EmailAddress = $"{Guid.NewGuid():N}@example.com",
            EncryptedRefreshToken = status == MailboxStatus.Deleted ? null : "cifrato",
            Status = status,
            BackfillCompleted = true,
        };
        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();
        return mailbox.Id;
    }

    /// <summary>Chiavi degli errori di un ValidationProblem.</summary>
    private static async Task<string[]> ErrorKeysAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
    }

    [Fact]
    public async Task Status_IsAnonymous_AndReturns200Async()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingApiKey_Returns401Async()
    {
        var response = await _factory.CreateClient().GetAsync(FiltersUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidFilter_Returns201Async()
    {
        var response = await _client.PostAsJsonAsync(FiltersUrl, ValidFilter());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("fatture@fornitore.it", created.GetProperty("senderAddress").GetString());
        Assert.Equal(2, created.GetProperty("processes").GetArrayLength());
    }

    [Theory]
    [InlineData("senderAddress", null, "senderAddress")]
    [InlineData("senderAddress", "non-una-mail", "senderAddress")]
    [InlineData("subjectValue", null, "subjectMatchType")]
    [InlineData("subjectMatchType", null, "subjectMatchType")]
    [InlineData("subjectMatchType", "EndsWith", "subjectMatchType")]
    [InlineData("name", "", "name")]
    [InlineData("attachmentSubfolder", "../fuori", "attachmentSubfolder")]
    public async Task InvalidFilterField_Returns400Async(string field, string? value, string expectedErrorKey)
    {
        var body = ValidFilter();
        body[field] = value;

        var response = await _client.PostAsJsonAsync(FiltersUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedErrorKey, await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task InvalidRegex_Returns400Async()
    {
        var body = ValidFilter();
        body["subjectMatchType"] = "Regex";
        body["subjectValue"] = "([non-chiusa";

        var response = await _client.PostAsJsonAsync(FiltersUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("subjectValue", await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task NoProcesses_Returns400Async()
    {
        var body = ValidFilter();
        body["processes"] = Array.Empty<string>();

        var response = await _client.PostAsJsonAsync(FiltersUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("processes", await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Reapply_WithProcessNotInFilter_Returns400Async()
    {
        var body = ValidFilter();
        body["processes"] = new[] { "Alert" };
        body["attachmentSubfolder"] = null;
        var created = await (await _client.PostAsJsonAsync(FiltersUrl, body)).Content.ReadFromJsonAsync<JsonElement>();
        var filterId = created.GetProperty("id").GetInt32();

        var response = await _client.PostAsJsonAsync($"{FiltersUrl}/{filterId}/reapply", new { processes = new[] { "AttachmentExtraction" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("processes", await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task ManualRun_StartsImmediately_ThenSecondStartReturns409Async()
    {
        var mailboxId = await SeedMailboxAsync();

        var first = await _client.PostAsJsonAsync(RunsUrl, new { mailboxId });
        var second = await _client.PostAsJsonAsync(RunsUrl, new { mailboxId });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var started = await first.Content.ReadFromJsonAsync<JsonElement>();
        var runId = started.GetProperty("runIds")[0].GetInt64();
        Assert.Contains(runId, _factory.Dispatcher.Dispatched);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var run = await _client.GetFromJsonAsync<JsonElement>($"{RunsUrl}/{runId}");
        Assert.Equal("Running", run.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ManualRun_OnBusyLock_Returns409Async()
    {
        var mailboxId = await SeedMailboxAsync();
        _factory.Lock.Occupy(mailboxId);

        var response = await _client.PostAsJsonAsync(RunsUrl, new { mailboxId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeletedMailbox_FilterCreationReturns409_AndDeleteIsLogicalAsync()
    {
        var mailboxId = await SeedMailboxAsync();

        var delete = await _client.DeleteAsync($"/api/v1/mailboxes/{mailboxId}");
        var body = ValidFilter();
        body["mailboxId"] = mailboxId;
        var create = await _client.PostAsJsonAsync(FiltersUrl, body);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        await using var db = _factory.NewContext();
        var mailbox = await db.Mailboxes.SingleAsync(m => m.Id == mailboxId);
        Assert.Equal(MailboxStatus.Deleted, mailbox.Status);
        Assert.Null(mailbox.EncryptedRefreshToken);
    }

    [Fact]
    public async Task UnconsumedAlerts_WhenNone_Returns404Async()
    {
        var response = await _client.GetAsync("/api/v1/alerts?consumed=false&senderAddress=nessuno@example.com");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConnectCallback_WithForgedState_RedirectsWithErrorAsync()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/mailboxes/connect/callback?code=abc&state=falso");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("oauth=error", response.Headers.Location!.ToString());
        Assert.Contains("invalid_state", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ConnectFlow_WithIssuedState_RegistersMailboxAsync()
    {
        var connect = await _client.GetFromJsonAsync<JsonElement>("/api/v1/mailboxes/connect");
        var url = new Uri(connect.GetProperty("authorizationUrl").GetString()!);
        var state = Uri.UnescapeDataString(url.Query.Split("state=")[1]);
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/v1/mailboxes/connect/callback?code=xyz&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("oauth=ok", response.Headers.Location!.ToString());
        await using var db = _factory.NewContext();
        var mailbox = await db.Mailboxes.SingleAsync(m => m.EmailAddress == "nuova@example.com");
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
        Assert.NotEqual("refresh-xyz", mailbox.EncryptedRefreshToken);
    }
}
