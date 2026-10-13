using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>
/// Flusso della purga (PurgeTrashedMessagesJob) con gli stessi componenti e nello stesso ordine: percorsi letti prima, righe eliminate,
/// poi file cancellati. La DELETE in blocco (ExecuteDeleteAsync) non gira su InMemory: qui la riga si elimina con Remove tracciato,
/// che innesca la stessa cascata sull'archivio; la query della soglia è quella del job (MessageProvider.TrashedBefore).
/// </summary>
public sealed class TrashedPurgeFlowTests : IDisposable
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

    [Fact]
    public async Task Purge_RemovesMessageArchiveRowAndFile_AndMissingFileDoesNotBlockAsync()
    {
        var threshold = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Directory.CreateDirectory(Path.Combine(_root, "sconti"));
        var presentFile = Path.Combine(_root, "sconti", "presente.eml");
        File.WriteAllText(presentFile, "x");
        var keptFile = Path.Combine(_root, "sconti", "recente.eml");
        File.WriteAllText(keptFile, "x");

        await using (var seed = _db.CreateDbContext())
        {
            seed.Messages.AddRange(
                NewMessage(1, threshold.AddDays(-1)),
                NewMessage(2, threshold.AddDays(-2)),
                NewMessage(3, threshold.AddDays(1)));
            seed.ArchivedMessageContents.AddRange(
                NewContent(1, "sconti/presente.eml"),
                NewContent(2, "sconti/manca.eml"),
                NewContent(3, "sconti/recente.eml"));
            await seed.SaveChangesAsync();
        }

        int deletedFiles;
        await using (var context = _db.CreateDbContext())
        {
            var archive = new ArchiveProvider(context, NullLogger<ArchiveProvider>.Instance);
            var messages = new MessageProvider(context, NullLogger<MessageProvider>.Instance);

            // Stesso ordine del job: percorsi prima della DELETE, file dopo
            var paths = await archive.GetPathsOfTrashedBeforeAsync(threshold, CancellationToken.None);
            var toPurge = await messages.TrashedBefore(threshold).AsTracking().ToListAsync();
            await context.ArchivedMessageContents.AsTracking().ToListAsync();
            context.Messages.RemoveRange(toPurge);
            await context.SaveChangesAsync();
            deletedFiles = archive.DeleteFiles(_root, paths);
        }

        Assert.Equal(1, deletedFiles);
        Assert.False(File.Exists(presentFile));
        Assert.True(File.Exists(keptFile));
        await using var check = _db.CreateDbContext();
        Assert.Equal([3L], await check.Messages.Select(m => m.Id).ToListAsync());
        Assert.Equal([3L], await check.ArchivedMessageContents.Select(a => a.MessageId).ToListAsync());
    }

    private static Message NewMessage(long id, DateTime trashedAtUtc) => new()
    {
        Id = id,
        MailboxId = 1,
        GmailMessageId = "g" + id,
        ThreadId = "t" + id,
        SenderAddress = "promo@negozio.it",
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
