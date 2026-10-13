using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;

namespace dr_mailroom_tests.Api;

/// <summary>
/// Mail di un filtro (paginazione dopo il confronto in memoria, stato di archiviazione), download del contenuto
/// archiviato (.eml e .txt, mai fuori dalla root) e "Elimina tutte" fino a una data.
/// </summary>
public sealed class FilterMessagesEndpointTests : IClassFixture<MailroomApiFactory>
{
    private const string Sender = "promo@negozio.it";

    private readonly MailroomApiFactory _factory;
    private readonly HttpClient _client;

    public FilterMessagesEndpointTests(MailroomApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthorizedClient();
    }

    private async Task<Mailbox> SeedMailboxAsync(bool trashAuthorized = false)
    {
        await using var db = _factory.NewContext();
        var mailbox = new Mailbox
        {
            EmailAddress = $"{Guid.NewGuid():N}@example.com",
            EncryptedRefreshToken = "cifrato",
            EncryptedTrashRefreshToken = trashAuthorized ? "cestino-cifrato" : null,
            Status = MailboxStatus.Active,
            BackfillCompleted = true,
        };
        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();
        return mailbox;
    }

    private async Task<Message> SeedMessageAsync(int mailboxId, string subject, DateTime receivedAtUtc, string sender = Sender, DateTime? trashedAtUtc = null)
    {
        await using var db = _factory.NewContext();
        var id = Guid.NewGuid().ToString("N");
        var message = new Message
        {
            MailboxId = mailboxId,
            GmailMessageId = id,
            ThreadId = "t-" + id,
            SenderAddress = sender,
            Subject = subject,
            ReceivedAtUtc = receivedAtUtc,
            TrashedAtUtc = trashedAtUtc,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    private async Task<int> SeedFilterAsync(int mailboxId, SubjectMatchType? matchType, string? value, bool enabled = true, params ProcessType[] processes)
    {
        await using var db = _factory.NewContext();
        var filter = new Filter
        {
            MailboxId = mailboxId,
            Name = "sconti " + Guid.NewGuid().ToString("N")[..6],
            SenderAddress = Sender,
            SubjectMatchType = matchType,
            SubjectValue = value,
            IsEnabled = enabled,
            Processes = [.. (processes.Length == 0 ? [ProcessType.Alert] : processes).Select(p => new FilterProcess { ProcessType = p })],
        };
        db.Filters.Add(filter);
        await db.SaveChangesAsync();
        return filter.Id;
    }

    /// <summary>Scrive un .eml sotto la root dell'host e lo registra come archiviato.</summary>
    private async Task ArchiveAsync(long messageId, string relativePath, string eml)
    {
        var fullPath = Path.Combine(_factory.AttachmentsRoot, relativePath);
        if (!relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, eml);
        }

        await using var db = _factory.NewContext();
        db.ArchivedMessageContents.Add(new ArchivedMessageContent
        {
            MessageId = messageId,
            RelativePath = relativePath,
            SizeBytes = eml.Length,
            Sha256 = new string('0', 64),
            ArchivedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static DateTime Day(int month, int day) => new(2026, month, day, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FilterMessages_ForMissingFilter_Returns404Async()
    {
        var response = await _client.GetAsync("/api/v1/filters/987654/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FilterMessages_WithInvalidPageSize_Returns400Async()
    {
        var mailbox = await SeedMailboxAsync();
        var filterId = await SeedFilterAsync(mailbox.Id, null, null);

        var response = await _client.GetAsync($"/api/v1/filters/{filterId}/messages?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FilterMessages_RegexMatch_PagesAfterMatching_WithArchiveStateAsync()
    {
        var mailbox = await SeedMailboxAsync();
        var first = await SeedMessageAsync(mailbox.Id, "Codice SCONTO10", Day(1, 1));
        await SeedMessageAsync(mailbox.Id, "Newsletter di gennaio", Day(1, 2));
        var second = await SeedMessageAsync(mailbox.Id, "Il tuo codice SCONTO20", Day(1, 3));
        var third = await SeedMessageAsync(mailbox.Id, "codice sconto30 in scadenza", Day(1, 4));
        await SeedMessageAsync(mailbox.Id, "Codice SCONTO99", Day(1, 5), trashedAtUtc: Day(2, 1));
        await SeedMessageAsync(mailbox.Id, "Codice SCONTO77", Day(1, 6), sender: "altro@negozio.it");
        var filterId = await SeedFilterAsync(mailbox.Id, SubjectMatchType.Regex, @"sconto\d+", enabled: false, ProcessType.ContentArchive);
        await ArchiveAsync(second.Id, $"t-{Guid.NewGuid():N}/b.eml", "x");

        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/filters/{filterId}/messages?pageSize=2");

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("missingArchiveCount").GetInt32());
        Assert.True(page.GetProperty("hasContentArchive").GetBoolean());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([third.Id, second.Id], items.Select(i => i.GetProperty("id").GetInt64()));
        Assert.Equal([false, true], items.Select(i => i.GetProperty("isArchived").GetBoolean()));

        var last = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/filters/{filterId}/messages?pageSize=2&page=2");
        Assert.Equal([first.Id], last.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()));

        var ascending = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/filters/{filterId}/messages?sortDir=Asc&pageSize=1");
        Assert.Equal(first.Id, ascending.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task FilterMessages_WithoutContentArchiveProcess_SaysSoAsync()
    {
        var mailbox = await SeedMailboxAsync();
        await SeedMessageAsync(mailbox.Id, "Offerta", Day(3, 1));
        var filterId = await SeedFilterAsync(mailbox.Id, null, null, true, ProcessType.Alert);

        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/filters/{filterId}/messages");

        Assert.False(page.GetProperty("hasContentArchive").GetBoolean());
        Assert.Equal(1, page.GetProperty("missingArchiveCount").GetInt32());
    }

    [Fact]
    public async Task Content_Eml_IsServedAsAttachment_WithNoSniffAsync()
    {
        var mailbox = await SeedMailboxAsync();
        var message = await SeedMessageAsync(mailbox.Id, "Codice", Day(4, 1));
        var eml = "From: promo@negozio.it\r\nSubject: Codice\r\nContent-Type: text/html\r\n\r\n<script>x</script>\r\n";
        await ArchiveAsync(message.Id, "sconti/20260401_codice.eml", eml);

        var response = await _client.GetAsync($"/api/v1/messages/{message.Id}/content?format=EML");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("20260401_codice.eml", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(eml, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Content_Txt_IsReadableTextOfTheEmlAsync()
    {
        var mailbox = await SeedMailboxAsync();
        var message = await SeedMessageAsync(mailbox.Id, "Codice", Day(4, 2));
        await ArchiveAsync(
            message.Id,
            "sconti/20260402_codice.eml",
            "From: promo@negozio.it\r\nSubject: Codice\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<p>Usa <b>SCONTO10</b></p>\r\n");

        var response = await _client.GetAsync($"/api/v1/messages/{message.Id}/content?format=txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("20260402_codice.txt", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Usa SCONTO10", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pdf")]
    [InlineData("1")]
    public async Task Content_WithInvalidFormat_Returns400Async(string format)
    {
        var response = await _client.GetAsync($"/api/v1/messages/1/content?format={format}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Content_NotArchived_Returns404Async()
    {
        var mailbox = await SeedMailboxAsync();
        var message = await SeedMessageAsync(mailbox.Id, "Codice", Day(4, 3));

        var response = await _client.GetAsync($"/api/v1/messages/{message.Id}/content?format=eml");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Content_PathOutsideRoot_Returns404Async()
    {
        var mailbox = await SeedMailboxAsync();
        var message = await SeedMessageAsync(mailbox.Id, "Codice", Day(4, 4));
        await ArchiveAsync(message.Id, "../../appsettings.json", "x");

        var response = await _client.GetAsync($"/api/v1/messages/{message.Id}/content?format=eml");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SenderMessages_WithReceivedBefore_CountsOnlyOlderMailAsync()
    {
        var mailbox = await SeedMailboxAsync();
        await SeedMessageAsync(mailbox.Id, "a", new DateTime(2026, 1, 1, 22, 59, 0, DateTimeKind.Utc));
        await SeedMessageAsync(mailbox.Id, "b", new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc));

        var page = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/messages?mailboxId={mailbox.Id}&senderAddress={Sender}&receivedBeforeUtc=2026-01-01T23:00:00Z&pageSize=1");

        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task TrashSender_WithReceivedBefore_CountsAndStoresTheLimitAsync()
    {
        var mailbox = await SeedMailboxAsync(trashAuthorized: true);
        await SeedMessageAsync(mailbox.Id, "a", Day(1, 1));
        await SeedMessageAsync(mailbox.Id, "b", Day(1, 2));
        await SeedMessageAsync(mailbox.Id, "c", Day(1, 3));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/messages/trash-sender",
            new { mailboxId = mailbox.Id, senderAddress = Sender, receivedBeforeUtc = "2026-01-02T23:00:00Z" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, started.GetProperty("messagesToTrash").GetInt32());

        await using var db = _factory.NewContext();
        var run = await db.Runs.SingleAsync(r => r.Id == started.GetProperty("runId").GetInt64());
        Assert.Equal(new DateTime(2026, 1, 2, 23, 0, 0, DateTimeKind.Utc), run.TrashReceivedBeforeUtc);
    }

    [Fact]
    public async Task TrashSender_WithReceivedBeforeMatchingNothing_Returns409Async()
    {
        var mailbox = await SeedMailboxAsync(trashAuthorized: true);
        await SeedMessageAsync(mailbox.Id, "a", Day(5, 1));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/messages/trash-sender",
            new { mailboxId = mailbox.Id, senderAddress = Sender, receivedBeforeUtc = "2026-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
