using System.IO.Compression;
using System.Net;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Gmail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dr_mailroom_tests.Gmail;

/// <summary>Verifica il client del cestino su risposte HTTP simulate: batchModify, ripiego su messages.trash, invalid_grant.</summary>
public class GmailTrashSessionTests
{
    /// <summary>Apre una sessione reale di GmailTrasher sopra l'handler finto, senza attese tra i tentativi.</summary>
    private static IGmailTrashSession OpenSession(FakeGoogleHandler handler)
    {
        var trasher = new GmailTrasher(
            Options.Create(new GmailOptions { ClientId = "id", ClientSecret = "secret", RedirectUri = "http://localhost/cb" }),
            new GmailRetrySettings(3, TimeSpan.Zero, TimeSpan.Zero),
            new FakeGoogleHttpClientFactory(handler),
            NullLoggerFactory.Instance);
        return trasher.OpenSession("fake-trash-refresh-token");
    }

    /// <summary>Corpo della richiesta come testo: la libreria Google lo invia compresso in gzip.</summary>
    private static string ReadBody(HttpRequestMessage request)
    {
        var bytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        if (request.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    [Fact]
    public async Task Trash_SendsOneBatchModifyWithTrashLabelAsync()
    {
        string? body = null;
        var handler = new FakeGoogleHandler((request, _) =>
        {
            body = ReadBody(request);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var session = OpenSession(handler);

        await session.TrashAsync(["a", "b", "c"], CancellationToken.None);

        Assert.Equal(1, handler.GmailCalls);
        Assert.Contains("/messages/batchModify", handler.GmailUrls[0], StringComparison.Ordinal);
        Assert.Contains("\"TRASH\"", body, StringComparison.Ordinal);
        Assert.Contains("\"a\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trash_BatchModifyRejected_FallsBackToOneTrashPerMessageAsync()
    {
        var handler = new FakeGoogleHandler((_, call) => call == 1
            ? FakeGoogleHandler.GoogleError(HttpStatusCode.BadRequest)
            : FakeGoogleHandler.Json(HttpStatusCode.OK, """{"id":"x","labelIds":["TRASH"]}"""));
        using var session = OpenSession(handler);

        await session.TrashAsync(["a", "b"], CancellationToken.None);

        Assert.Equal(3, handler.GmailCalls);
        Assert.EndsWith("/messages/a/trash", handler.GmailUrls[1], StringComparison.Ordinal);
        Assert.EndsWith("/messages/b/trash", handler.GmailUrls[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trash_FallbackSkipsMessagesAlreadyGoneAsync()
    {
        var handler = new FakeGoogleHandler((_, call) => call switch
        {
            1 => FakeGoogleHandler.GoogleError(HttpStatusCode.BadRequest),
            2 => FakeGoogleHandler.GoogleError(HttpStatusCode.NotFound),
            _ => FakeGoogleHandler.Json(HttpStatusCode.OK, """{"id":"b"}"""),
        });
        using var session = OpenSession(handler);

        await session.TrashAsync(["a", "b"], CancellationToken.None);

        Assert.Equal(3, handler.GmailCalls);
    }

    [Fact]
    public async Task Trash_InvalidGrant_ThrowsTokenRevokedAsync()
    {
        var handler = new FakeGoogleHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent))
        {
            TokenResponder = () => FakeGoogleHandler.Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""),
        };
        using var session = OpenSession(handler);

        await Assert.ThrowsAsync<TokenRevokedException>(() => session.TrashAsync(["a"], CancellationToken.None));
        Assert.Equal(0, handler.GmailCalls);
    }

    [Fact]
    public async Task Trash_MoreThanGmailLimit_IsRejectedBeforeCallingGmailAsync()
    {
        var handler = new FakeGoogleHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var session = OpenSession(handler);
        var ids = Enumerable.Range(0, GmailConstants.MaxBatchModifyIds + 1).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.TrashAsync(ids, CancellationToken.None));
        Assert.Equal(0, handler.GmailCalls);
    }
}
