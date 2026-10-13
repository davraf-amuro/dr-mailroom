using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>Percorsi dei contenuti archiviati: mai fuori dalla root; purga dei file delle mail cestinate oltre la soglia.</summary>
public sealed class ArchiveProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dr-mailroom-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryMailroomDb _db = new();

    /// <summary>Rimuove la cartella temporanea del test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData("sconti/a.eml", true)]
    [InlineData("a.eml", true)]
    [InlineData("../fuori.eml", false)]
    [InlineData("sconti/../../fuori.eml", false)]
    [InlineData("", false)]
    public void ResolveUnderRoot_AcceptsOnlyPathsInsideRoot(string relativePath, bool accepted)
    {
        var resolved = ArchiveProvider.ResolveUnderRoot(_root, relativePath);

        Assert.Equal(accepted, resolved is not null);
        if (resolved is not null)
        {
            Assert.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, resolved, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResolveUnderRoot_RejectsAbsolutePath()
    {
        Assert.Null(ArchiveProvider.ResolveUnderRoot(_root, Path.Combine(Path.GetTempPath(), "x.eml")));
    }

    [Fact]
    public void DeleteFiles_DeletesInsideRoot_SkipsMissingAndOutsideRoot()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sconti"));
        File.WriteAllText(Path.Combine(_root, "sconti", "a.eml"), "x");
        var outside = Path.Combine(Path.GetDirectoryName(_root)!, Guid.NewGuid().ToString("N") + ".eml");
        File.WriteAllText(outside, "x");

        try
        {
            using var context = _db.CreateDbContext();
            var provider = new ArchiveProvider(context, NullLogger<ArchiveProvider>.Instance);

            var deleted = provider.DeleteFiles(_root, ["sconti/a.eml", "sconti/manca.eml", "../" + Path.GetFileName(outside)]);

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(Path.Combine(_root, "sconti", "a.eml")));
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task GetPathsOfTrashedBefore_ReturnsOnlyMailTrashedBeforeThresholdAsync()
    {
        var threshold = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var context = _db.CreateDbContext())
        {
            context.Messages.AddRange(
                NewMessage(1, trashedAtUtc: threshold.AddDays(-1)),
                NewMessage(2, trashedAtUtc: threshold.AddDays(1)),
                NewMessage(3, trashedAtUtc: null));
            context.ArchivedMessageContents.AddRange(
                NewContent(1, "vecchia.eml"), NewContent(2, "recente.eml"), NewContent(3, "attiva.eml"));
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateDbContext();
        var paths = await new ArchiveProvider(read, NullLogger<ArchiveProvider>.Instance)
            .GetPathsOfTrashedBeforeAsync(threshold, CancellationToken.None);

        Assert.Equal(["vecchia.eml"], paths);
    }

    [Fact]
    public async Task GetAsync_IgnoresTrashedMailAsync()
    {
        await using (var context = _db.CreateDbContext())
        {
            context.Messages.AddRange(NewMessage(1, trashedAtUtc: DateTime.UtcNow), NewMessage(2, trashedAtUtc: null));
            context.ArchivedMessageContents.AddRange(NewContent(1, "a.eml"), NewContent(2, "b.eml"));
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateDbContext();
        var provider = new ArchiveProvider(read, NullLogger<ArchiveProvider>.Instance);

        Assert.Null(await provider.GetAsync(1, CancellationToken.None));
        Assert.Equal("b.eml", (await provider.GetAsync(2, CancellationToken.None))?.RelativePath);
    }

    [Fact]
    public async Task GetArchivedMessageIdsBySender_FiltersBySenderMailboxAndTrashAsync()
    {
        await using (var context = _db.CreateDbContext())
        {
            context.Messages.AddRange(
                NewMessage(1, trashedAtUtc: null),
                NewMessage(2, trashedAtUtc: DateTime.UtcNow),
                NewMessage(3, trashedAtUtc: null, mailboxId: 2),
                NewMessage(4, trashedAtUtc: null, sender: "altro@negozio.it"),
                NewMessage(5, trashedAtUtc: null));
            context.ArchivedMessageContents.AddRange(
                NewContent(1, "a.eml"), NewContent(2, "b.eml"), NewContent(3, "c.eml"), NewContent(4, "d.eml"));
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateDbContext();
        var provider = new ArchiveProvider(read, NullLogger<ArchiveProvider>.Instance);

        Assert.Equal([1L], await provider.GetArchivedMessageIdsBySenderAsync(1, "  PROMO@negozio.it ", CancellationToken.None));
        Assert.Equal([1L, 3L], (await provider.GetArchivedMessageIdsBySenderAsync(null, "promo@negozio.it", CancellationToken.None)).Order());
    }

    private static Message NewMessage(long id, DateTime? trashedAtUtc, int mailboxId = 1, string sender = "promo@negozio.it") => new()
    {
        Id = id,
        MailboxId = mailboxId,
        GmailMessageId = "g" + id,
        ThreadId = "t" + id,
        SenderAddress = sender,
        ReceivedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        TrashedAtUtc = trashedAtUtc,
    };

    private static ArchivedMessageContent NewContent(long messageId, string relativePath) => new()
    {
        MessageId = messageId,
        RelativePath = relativePath,
        SizeBytes = 1,
        Sha256 = new string('0', 64),
        ArchivedAtUtc = DateTime.UtcNow,
    };
}
