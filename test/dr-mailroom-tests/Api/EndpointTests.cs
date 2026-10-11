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
    private const string StatsUrl = "/api/v1/stats";

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

    /// <summary>Avvia un giro manuale (il dispatcher finto non lo esegue: resta Running senza job) e ne restituisce l'Id.</summary>
    private async Task<long> StartRunAsync(int mailboxId)
    {
        var response = await _client.PostAsJsonAsync(RunsUrl, new { mailboxId });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<JsonElement>();
        return started.GetProperty("runIds")[0].GetInt64();
    }

    /// <summary>Stato del giro letto dall'API.</summary>
    private async Task<string?> RunStatusAsync(long runId) =>
        (await _client.GetFromJsonAsync<JsonElement>($"{RunsUrl}/{runId}")).GetProperty("status").GetString();

    [Fact]
    public async Task CancelRun_WithoutJob_ClosesCancelled_ThenSecondCancelReturns409Async()
    {
        var runId = await StartRunAsync(await SeedMailboxAsync());

        var first = await _client.PostAsync($"{RunsUrl}/{runId}/cancel", null);
        var second = await _client.PostAsync($"{RunsUrl}/{runId}/cancel", null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal("Cancelled", await RunStatusAsync(runId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CancelRun_Unknown_Returns404Async()
    {
        var response = await _client.PostAsync($"{RunsUrl}/999999/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelAllRuns_CancelsEveryRunningRunAsync()
    {
        var runId = await StartRunAsync(await SeedMailboxAsync());

        var response = await _client.PostAsync($"{RunsUrl}/cancel", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(runId, body.GetProperty("runIds").EnumerateArray().Select(e => e.GetInt64()));
        Assert.Equal("Cancelled", await RunStatusAsync(runId));
    }

    [Fact]
    public async Task PauseMailbox_CancelsRuns_BlocksManualRun_UntilResumeAsync()
    {
        var mailboxId = await SeedMailboxAsync();
        var runId = await StartRunAsync(mailboxId);

        var pause = await _client.PostAsync($"/api/v1/mailboxes/{mailboxId}/pause", null);
        var pauseAgain = await _client.PostAsync($"/api/v1/mailboxes/{mailboxId}/pause", null);
        var runWhilePaused = await _client.PostAsJsonAsync(RunsUrl, new { mailboxId });

        Assert.Equal(HttpStatusCode.NoContent, pause.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, pauseAgain.StatusCode);
        Assert.Equal("Cancelled", await RunStatusAsync(runId));
        Assert.Equal(HttpStatusCode.Conflict, runWhilePaused.StatusCode);

        var resume = await _client.PostAsync($"/api/v1/mailboxes/{mailboxId}/resume", null);
        var resumeAgain = await _client.PostAsync($"/api/v1/mailboxes/{mailboxId}/resume", null);

        Assert.Equal(HttpStatusCode.NoContent, resume.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, resumeAgain.StatusCode);
        await using var db = _factory.NewContext();
        Assert.Equal(MailboxStatus.Active, (await db.Mailboxes.SingleAsync(m => m.Id == mailboxId)).Status);
    }

    [Fact]
    public async Task PausedMailbox_ReapplyReturns409Async()
    {
        var mailboxId = await SeedMailboxAsync(MailboxStatus.Disabled);
        var body = ValidFilter();
        body["mailboxId"] = mailboxId;
        var created = await _client.PostAsJsonAsync(FiltersUrl, body);
        var filterId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await _client.PostAsJsonAsync($"{FiltersUrl}/{filterId}/reapply", new { processes = new[] { "AttachmentExtraction" } });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(MailboxStatus.Deleted, "pause")]
    [InlineData(MailboxStatus.TokenRevoked, "pause")]
    [InlineData(MailboxStatus.TokenRevoked, "resume")]
    public async Task PauseOrResume_OnIncompatibleStatus_Returns409Async(MailboxStatus status, string action)
    {
        var mailboxId = await SeedMailboxAsync(status);

        var response = await _client.PostAsync($"/api/v1/mailboxes/{mailboxId}/{action}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    public async Task PauseOrResume_UnknownMailbox_Returns404Async(string action)
    {
        var response = await _client.PostAsync($"/api/v1/mailboxes/999999/{action}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    [Fact]
    public async Task SenderOverview_ReturnsPagedRowsWithOldestAndNewestMailAsync()
    {
        var mailboxId = await SeedMailboxAsync();
        await using (var db = _factory.NewContext())
        {
            db.Messages.AddRange(
                new Message { MailboxId = mailboxId, GmailMessageId = "ov-1", ThreadId = "t", SenderAddress = "a@x.it", Subject = "vecchia", ReceivedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Message { MailboxId = mailboxId, GmailMessageId = "ov-2", ThreadId = "t", SenderAddress = "a@x.it", Subject = "recente", ReceivedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsSent = true },
                new Message { MailboxId = mailboxId, GmailMessageId = "ov-3", ThreadId = "t", SenderAddress = "b@x.it", Subject = "b", ReceivedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        }

        var page = await _client.GetFromJsonAsync<JsonElement>($"{StatsUrl}/senders/overview?mailboxId={mailboxId}&pageSize=1");
        var years = await _client.GetFromJsonAsync<JsonElement>($"{StatsUrl}/years?mailboxId={mailboxId}&senderAddress=A@X.IT");

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("pageSize").GetInt32());
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("a@x.it", row.GetProperty("senderAddress").GetString());
        Assert.Equal("vecchia", row.GetProperty("firstSubject").GetString());
        Assert.Equal("recente", row.GetProperty("lastSubject").GetString());
        Assert.Equal(1, row.GetProperty("sentCount").GetInt32());
        Assert.Equal([2026, 2024], years.EnumerateArray().Select(y => y.GetProperty("year").GetInt32()));
    }

    [Theory]
    [InlineData("", "mailboxId")]
    [InlineData("mailboxId=0", "mailboxId")]
    [InlineData("mailboxId=1&pageSize=101", "pageSize")]
    [InlineData("mailboxId=1&pageSize=0", "pageSize")]
    [InlineData("mailboxId=1&page=0", "page")]
    [InlineData("mailboxId=1&year=1900", "year")]
    public async Task SenderOverview_InvalidQuery_Returns400Async(string query, string expectedErrorKey)
    {
        var response = await _client.GetAsync($"{StatsUrl}/senders/overview?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedErrorKey, await ErrorKeysAsync(response));
    }

    [Theory]
    [InlineData("senders/overview")]
    [InlineData("years")]
    public async Task Stats_UnknownMailbox_Returns404Async(string path)
    {
        var response = await _client.GetAsync($"{StatsUrl}/{path}?mailboxId=999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
