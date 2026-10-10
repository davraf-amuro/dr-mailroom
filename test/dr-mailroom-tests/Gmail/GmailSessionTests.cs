using System.Net;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Gmail;
using Google;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dr_mailroom_tests.Gmail;

/// <summary>Verifica il client Gmail su risposte HTTP simulate: errori di dominio, ritentativi, filtri su Spam/Cestino.</summary>
public class GmailSessionTests
{
    private const int MaxAttempts = 3;

    /// <summary>Apre una sessione reale di GmailReader sopra l'handler finto, senza attese tra i tentativi.</summary>
    private static IGmailSession OpenSession(FakeGoogleHandler handler, bool includeSpamTrash = false)
    {
        var reader = new GmailReader(
            Options.Create(new GmailOptions { ClientId = "id", ClientSecret = "secret", RedirectUri = "http://localhost/cb" }),
            Options.Create(new ProcessingOptions { IncludeSpamTrash = includeSpamTrash }),
            new GmailRetrySettings(MaxAttempts, TimeSpan.Zero, TimeSpan.Zero),
            new FakeGoogleHttpClientFactory(handler),
            NullLoggerFactory.Instance);
        return reader.OpenSession("fake-refresh-token");
    }

    [Fact]
    public async Task ListChangesSince_Http404_ThrowsHistoryExpiredAsync()
    {
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.GoogleError(HttpStatusCode.NotFound));
        using var session = OpenSession(handler);

        var ex = await Assert.ThrowsAsync<HistoryExpiredException>(() => session.ListChangesSinceAsync(42, CancellationToken.None));

        Assert.Equal(42UL, ex.HistoryId);
        Assert.Equal(1, handler.GmailCalls);
    }

    [Fact]
    public async Task Http429_IsRetriedThenSucceedsAsync()
    {
        var handler = new FakeGoogleHandler((_, call) => call == 1
            ? FakeGoogleHandler.GoogleError(HttpStatusCode.TooManyRequests)
            : FakeGoogleHandler.Json(HttpStatusCode.OK, """{"emailAddress":"a@b.c","historyId":"777"}"""));
        using var session = OpenSession(handler);

        var historyId = (await session.GetProfileAsync(CancellationToken.None)).HistoryId;

        Assert.Equal(777UL, historyId);
        Assert.Equal(2, handler.GmailCalls);
    }

    [Fact]
    public async Task GetProfile_ReturnsHistoryIdAndMessagesTotalAsync()
    {
        var handler = new FakeGoogleHandler((_, _) =>
            FakeGoogleHandler.Json(HttpStatusCode.OK, """{"emailAddress":"a@b.c","historyId":"777","messagesTotal":48210}"""));
        using var session = OpenSession(handler);

        var profile = await session.GetProfileAsync(CancellationToken.None);

        Assert.Equal(new GmailProfile(777, 48210), profile);
    }

    [Theory]
    [InlineData("rateLimitExceeded")]
    [InlineData("userRateLimitExceeded")]
    public async Task Http403_QuotaExceeded_IsRetriedThenSucceedsAsync(string reason)
    {
        var handler = new FakeGoogleHandler((_, call) => call == 1
            ? FakeGoogleHandler.GoogleError(HttpStatusCode.Forbidden, reason)
            : FakeGoogleHandler.Json(HttpStatusCode.OK, """{"emailAddress":"a@b.c","historyId":"777"}"""));
        using var session = OpenSession(handler);

        var historyId = (await session.GetProfileAsync(CancellationToken.None)).HistoryId;

        Assert.Equal(777UL, historyId);
        Assert.Equal(2, handler.GmailCalls);
    }

    [Fact]
    public async Task Http403_NotQuota_IsNotRetriedAsync()
    {
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.GoogleError(HttpStatusCode.Forbidden, "insufficientPermissions"));
        using var session = OpenSession(handler);

        await Assert.ThrowsAsync<GoogleApiException>(() => session.GetProfileAsync(CancellationToken.None));

        Assert.Equal(1, handler.GmailCalls);
    }

    [Fact]
    public async Task Http500_Persistent_StopsAfterMaxAttemptsAsync()
    {
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.GoogleError(HttpStatusCode.InternalServerError));
        using var session = OpenSession(handler);

        await Assert.ThrowsAsync<GoogleApiException>(() => session.GetProfileAsync(CancellationToken.None));

        Assert.Equal(MaxAttempts, handler.GmailCalls);
    }

    [Fact]
    public async Task InvalidGrant_ThrowsTokenRevokedAsync()
    {
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.Json(HttpStatusCode.OK, "{}"))
        {
            TokenResponder = () => FakeGoogleHandler.Json(
                HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Token has been expired or revoked."}"""),
        };
        using var session = OpenSession(handler);

        await Assert.ThrowsAsync<TokenRevokedException>(() => session.GetProfileAsync(CancellationToken.None));
        Assert.Equal(0, handler.GmailCalls);
    }

    [Fact]
    public async Task ListChangesSince_SkipsSpamTrash_DeduplicatesAcrossPages_ReturnsLatestHistoryIdAsync()
    {
        const string Page1 = """
            {"history":[
              {"id":"101","messagesAdded":[{"message":{"id":"m1","labelIds":["INBOX"]}}]},
              {"id":"102","messagesAdded":[{"message":{"id":"spam1","labelIds":["SPAM"]}}]}
            ],"nextPageToken":"p2","historyId":"150"}
            """;
        const string Page2 = """
            {"history":[
              {"id":"103","messagesAdded":[{"message":{"id":"m1","labelIds":["INBOX"]}},{"message":{"id":"m2","labelIds":["CATEGORY_UPDATES"]}}]},
              {"id":"104","messagesAdded":[{"message":{"id":"trash1","labelIds":["TRASH"]}}]}
            ],"historyId":"160"}
            """;
        var handler = new FakeGoogleHandler((request, _) => FakeGoogleHandler.Json(
            HttpStatusCode.OK, request.RequestUri!.Query.Contains("pageToken=p2", StringComparison.Ordinal) ? Page2 : Page1));
        using var session = OpenSession(handler);

        var changes = await session.ListChangesSinceAsync(100, CancellationToken.None);

        Assert.Equal(["m1", "m2"], changes.AddedMessageIds);
        Assert.Equal(160UL, changes.LatestHistoryId);
        Assert.All(handler.GmailUrls, url => Assert.Contains("historyTypes=messageAdded", url));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListMessageIdsPage_RespectsIncludeSpamTrashAsync(bool includeSpamTrash)
    {
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.Json(
            HttpStatusCode.OK, """{"messages":[{"id":"a","threadId":"t"},{"id":"b","threadId":"t"}],"nextPageToken":"next"}"""));
        using var session = OpenSession(handler, includeSpamTrash);

        var page = await session.ListMessageIdsPageAsync(null, CancellationToken.None);

        Assert.Equal(["a", "b"], page.MessageIds);
        Assert.Equal("next", page.NextPageToken);

        // false è il default dell'API: la libreria omette il parametro, quindi Spam e Cestino restano esclusi (D6)
        var url = handler.GmailUrls.Single();
        Assert.Equal(includeSpamTrash, url.Contains("includeSpamTrash=true", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetMetadata_ParsesSenderSubjectDateAndAttachmentsAsync()
    {
        const string Body = """
            {"id":"m1","threadId":"t1","labelIds":["INBOX","UNREAD"],"internalDate":"1759622400000",
             "payload":{"mimeType":"multipart/mixed","headers":[
               {"name":"From","value":"Mario Rossi <Mario.Rossi@Example.COM>"},
               {"name":"Subject","value":"Fattura 2026/42"}]}}
            """;
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.Json(HttpStatusCode.OK, Body));
        using var session = OpenSession(handler);

        var metadata = await session.GetMetadataAsync("m1", CancellationToken.None);

        Assert.Equal("mario.rossi@example.com", metadata.SenderAddress);
        Assert.Equal("Mario Rossi", metadata.SenderName);
        Assert.Equal("Fattura 2026/42", metadata.Subject);
        Assert.Equal(new DateTime(2025, 10, 5, 0, 0, 0, DateTimeKind.Utc), metadata.ReceivedAtUtc);
        Assert.Equal(["INBOX", "UNREAD"], metadata.LabelIds);
        Assert.True(metadata.HasAttachments);
        Assert.Contains("format=metadata", handler.GmailUrls.Single());
        Assert.Contains("metadataHeaders=Date", handler.GmailUrls.Single());
        Assert.Equal(ReceivedDateSource.InternalDate, metadata.ReceivedDateSource);
    }

    [Fact]
    public async Task GetMetadata_WithoutInternalDate_UsesDateHeaderAsync()
    {
        const string Body = """
            {"id":"m2","threadId":"t2","labelIds":["SPAM"],
             "payload":{"mimeType":"text/plain","headers":[
               {"name":"From","value":"spam@example.com"},
               {"name":"Date","value":"Mon, 20 Dec 2021 11:48:00 +0100"}]}}
            """;
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.Json(HttpStatusCode.OK, Body));
        using var session = OpenSession(handler);

        var metadata = await session.GetMetadataAsync("m2", CancellationToken.None);

        Assert.Equal(new DateTime(2021, 12, 20, 10, 48, 0, DateTimeKind.Utc), metadata.ReceivedAtUtc);
        Assert.Equal(ReceivedDateSource.DateHeader, metadata.ReceivedDateSource);
        Assert.Null(metadata.RawInternalDate);
    }

    [Fact]
    public async Task GetAttachmentParts_WalksNestedParts_AndDownloadsInlineDataAsync()
    {
        const string Body = """
            {"id":"m1","payload":{"partId":"","mimeType":"multipart/mixed","parts":[
              {"partId":"0","mimeType":"text/plain","filename":"","body":{"size":10}},
              {"partId":"1","mimeType":"multipart/related","filename":"","parts":[
                {"partId":"1.1","mimeType":"application/pdf","filename":"fattura.pdf","body":{"attachmentId":"att-1","size":2048}},
                {"partId":"1.2","mimeType":"text/plain","filename":"nota.txt","body":{"data":"Y2lhbw","size":4}}]}]}}
            """;
        var handler = new FakeGoogleHandler((_, _) => FakeGoogleHandler.Json(HttpStatusCode.OK, Body));
        using var session = OpenSession(handler);

        var parts = await session.GetAttachmentPartsAsync("m1", CancellationToken.None);
        var inline = await session.DownloadAttachmentAsync("m1", parts[1], CancellationToken.None);

        Assert.Equal(["1.1", "1.2"], parts.Select(p => p.PartId));
        Assert.Equal("att-1", parts[0].AttachmentId);
        Assert.Equal("ciao", System.Text.Encoding.UTF8.GetString(inline));
    }

    [Theory]
    [InlineData("Mario Rossi <Mario.Rossi@Example.COM>", "mario.rossi@example.com", "Mario Rossi")]
    [InlineData("noreply@Fornitore.it", "noreply@fornitore.it", null)]
    [InlineData("indirizzo non valido", "indirizzo non valido", null)]
    [InlineData(null, "", null)]
    public void SenderParser_ExtractsLowercaseAddress(string? header, string address, string? name)
    {
        var parsed = SenderParser.Parse(header);

        Assert.Equal(address, parsed.Address);
        Assert.Equal(name, parsed.DisplayName);
    }
}
