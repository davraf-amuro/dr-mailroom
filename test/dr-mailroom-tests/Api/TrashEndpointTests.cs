using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace dr_mailroom_tests.Api;

/// <summary>
/// "Elimina tutte" (D-T1 bis): consenso separato per il cestino, salvato solo per l'account della casella; avvio del
/// giro con 400/404/409; mail cestinate escluse dall'elenco del mittente.
/// </summary>
public sealed class TrashEndpointTests : IClassFixture<MailroomApiFactory>
{
    private const string MessagesUrl = "/api/v1/messages";
    private const string TrashSenderUrl = "/api/v1/messages/trash-sender";
    private const string Spammer = "no-reply@spam.example";

    private readonly MailroomApiFactory _factory;
    private readonly HttpClient _client;

    public TrashEndpointTests(MailroomApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthorizedClient();
    }

    /// <summary>Casella attiva nel DB InMemory dell'host, con o senza token del cestino.</summary>
    private async Task<Mailbox> SeedMailboxAsync(bool trashAuthorized = false, MailboxStatus status = MailboxStatus.Active)
    {
        await using var db = _factory.NewContext();
        var mailbox = new Mailbox
        {
            EmailAddress = $"{Guid.NewGuid():N}@example.com",
            EncryptedRefreshToken = "cifrato",
            EncryptedTrashRefreshToken = trashAuthorized ? "cestino-cifrato" : null,
            Status = status,
            BackfillCompleted = true,
        };
        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();
        return mailbox;
    }

    /// <summary>Mail indicizzate del mittente nella casella.</summary>
    private async Task SeedMessagesAsync(int mailboxId, int count, bool isSent = false, DateTime? trashedAtUtc = null)
    {
        await using var db = _factory.NewContext();
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid().ToString("N");
            db.Messages.Add(new Message
            {
                MailboxId = mailboxId,
                GmailMessageId = id,
                ThreadId = "t-" + id,
                SenderAddress = Spammer,
                Subject = "Offerta",
                ReceivedAtUtc = new DateTime(2026, 10, 1, 8, i, 0, DateTimeKind.Utc),
                IsSent = isSent,
                TrashedAtUtc = trashedAtUtc,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>State dell'URL di consenso restituito dall'API.</summary>
    private static string StateOf(JsonElement connect) =>
        QueryHelpers.ParseQuery(new Uri(connect.GetProperty("authorizationUrl").GetString()!).Query)["state"].ToString();

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task TrashAuthorization_ForMissingMailbox_Returns404Async()
    {
        var response = await _client.GetAsync("/api/v1/mailboxes/987654/trash-authorization");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TrashAuthorization_ForPausedMailbox_Returns409Async()
    {
        var mailbox = await SeedMailboxAsync(status: MailboxStatus.Disabled);

        var response = await _client.GetAsync($"/api/v1/mailboxes/{mailbox.Id}/trash-authorization");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TrashAuthorizationFlow_WithMatchingAccount_SavesOnlyTrashToken_AndReturnsToSenderViewAsync()
    {
        var mailbox = await SeedMailboxAsync();
        _factory.Authorizer.ConnectedEmail = mailbox.EmailAddress.ToUpperInvariant();

        var connect = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/mailboxes/{mailbox.Id}/trash-authorization?sender={Uri.EscapeDataString(Spammer)}");
        Assert.Contains("purpose=Trash", connect.GetProperty("authorizationUrl").GetString());

        var response = await NoRedirectClient().GetAsync(
            $"/api/v1/mailboxes/connect/callback?code=xyz&state={Uri.EscapeDataString(StateOf(connect))}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("/stats/messages", location.AbsolutePath);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(mailbox.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), query["mailboxId"].ToString());
        Assert.Equal(Spammer, query["sender"].ToString());
        Assert.Equal("ok", query["oauth"].ToString());
        Assert.Equal(OAuthPurpose.Trash, _factory.Authorizer.ExchangedPurposes[^1]);

        await using var db = _factory.NewContext();
        var saved = await db.Mailboxes.SingleAsync(m => m.Id == mailbox.Id);
        Assert.NotNull(saved.EncryptedTrashRefreshToken);
        Assert.NotEqual("refresh-Trash-xyz", saved.EncryptedTrashRefreshToken);
        Assert.Equal("cifrato", saved.EncryptedRefreshToken);
    }

    [Fact]
    public async Task TrashAuthorizationFlow_WithDifferentAccount_IsRejectedAsync()
    {
        var mailbox = await SeedMailboxAsync();
        _factory.Authorizer.ConnectedEmail = "altro-account@example.com";

        var connect = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/mailboxes/{mailbox.Id}/trash-authorization?sender={Uri.EscapeDataString(Spammer)}");
        var response = await NoRedirectClient().GetAsync(
            $"/api/v1/mailboxes/connect/callback?code=xyz&state={Uri.EscapeDataString(StateOf(connect))}");

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("error", query["oauth"].ToString());
        Assert.Equal("trash_mailbox_mismatch", query["reason"].ToString());
        await using var db = _factory.NewContext();
        Assert.Null((await db.Mailboxes.SingleAsync(m => m.Id == mailbox.Id)).EncryptedTrashRefreshToken);
    }

    [Fact]
    public async Task TrashSender_WithoutSender_Returns400Async()
    {
        var mailbox = await SeedMailboxAsync(trashAuthorized: true);

        var response = await _client.PostAsJsonAsync(TrashSenderUrl, new { mailboxId = mailbox.Id, senderAddress = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TrashSender_ForMissingMailbox_Returns404Async()
    {
        var response = await _client.PostAsJsonAsync(TrashSenderUrl, new { mailboxId = 987654, senderAddress = Spammer });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TrashSender_WithoutTrashAuthorization_Returns409Async()
    {
        var mailbox = await SeedMailboxAsync();
        await SeedMessagesAsync(mailbox.Id, 2);

        var response = await _client.PostAsJsonAsync(TrashSenderUrl, new { mailboxId = mailbox.Id, senderAddress = Spammer });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TrashSender_WithNothingToTrash_Returns409Async()
    {
        var mailbox = await SeedMailboxAsync(trashAuthorized: true);
        await SeedMessagesAsync(mailbox.Id, 1, isSent: true);

        var response = await _client.PostAsJsonAsync(TrashSenderUrl, new { mailboxId = mailbox.Id, senderAddress = Spammer });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TrashSender_Authorized_Returns202_AndDispatchesRunForTheSenderAsync()
    {
        var mailbox = await SeedMailboxAsync(trashAuthorized: true);
        await SeedMessagesAsync(mailbox.Id, 2);
        await SeedMessagesAsync(mailbox.Id, 1, isSent: true);
        await SeedMessagesAsync(mailbox.Id, 1, trashedAtUtc: DateTime.UtcNow);

        var response = await _client.PostAsJsonAsync(TrashSenderUrl, new { mailboxId = mailbox.Id, senderAddress = Spammer.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, started.GetProperty("messagesToTrash").GetInt32());
        var runId = started.GetProperty("runId").GetInt64();
        Assert.Contains(runId, _factory.Dispatcher.Dispatched);

        var run = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/runs/{runId}");
        Assert.Equal("TrashSender", run.GetProperty("trigger").GetString());
        Assert.Equal(Spammer, run.GetProperty("senderAddress").GetString());
    }

    [Fact]
    public async Task SenderMessages_ExcludeTrashedMailAsync()
    {
        var mailbox = await SeedMailboxAsync();
        await SeedMessagesAsync(mailbox.Id, 2);
        await SeedMessagesAsync(mailbox.Id, 3, trashedAtUtc: DateTime.UtcNow);

        var page = await _client.GetFromJsonAsync<JsonElement>(
            $"{MessagesUrl}?mailboxId={mailbox.Id}&senderAddress={Uri.EscapeDataString(Spammer)}");

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task MailboxDetail_ExposesTrashAuthorizedAsync()
    {
        var authorized = await SeedMailboxAsync(trashAuthorized: true);
        var notAuthorized = await SeedMailboxAsync();

        var first = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/mailboxes/{authorized.Id}");
        var second = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/mailboxes/{notAuthorized.Id}");

        Assert.True(first.GetProperty("trashAuthorized").GetBoolean());
        Assert.False(second.GetProperty("trashAuthorized").GetBoolean());
    }
}
