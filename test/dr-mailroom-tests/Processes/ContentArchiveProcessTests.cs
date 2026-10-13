using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Processes;
using dr_mailroom_tests.Ingestion;
using dr_mailroom_tests.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dr_mailroom_tests.Processes;

/// <summary>Archiviazione del contenuto (.eml): cartella del filtro, idempotenza, limite di dimensione, nessun file orfano.</summary>
public sealed class ContentArchiveProcessTests : IDisposable
{
    private const long MessageId = 200;
    private const long MaxSize = 1_000;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dr-mailroom-tests", Guid.NewGuid().ToString("N"));
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>Rimuove la cartella temporanea del test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private MailroomDbContext NewContext() => new(
        new DbContextOptionsBuilder<MailroomDbContext>().UseInMemoryDatabase(_databaseName).Options,
        NullLoggerFactory.Instance);

    private static Message NewMessage(string? subject = "Il tuo codice sconto") => new()
    {
        Id = MessageId,
        MailboxId = 1,
        GmailMessageId = "g1",
        SenderAddress = "promo@negozio.it",
        Subject = subject,
        ReceivedAtUtc = new DateTime(2026, 10, 1, 8, 30, 15, DateTimeKind.Utc),
    };

    /// <summary>Due filtri corrispondenti: il primo estrae allegati in "fatture", il secondo archivia in "sconti".</summary>
    private static FilterMatchResult Match() => new(
        [
            new Filter
            {
                Id = 1,
                SenderAddress = "promo@negozio.it",
                AttachmentSubfolder = "fatture",
                Processes = [new FilterProcess { FilterId = 1, ProcessType = ProcessType.AttachmentExtraction }],
            },
            new Filter
            {
                Id = 2,
                SenderAddress = "promo@negozio.it",
                AttachmentSubfolder = "sconti",
                Processes = [new FilterProcess { FilterId = 2, ProcessType = ProcessType.ContentArchive }],
            },
        ],
        new HashSet<ProcessType> { ProcessType.AttachmentExtraction, ProcessType.ContentArchive });

    private async Task<ProcessExecutionStatus> RunAsync(FakeGmailSession session, Message? message = null)
    {
        await using var db = NewContext();
        var process = new ContentArchiveProcess(
            Options.Create(new AttachmentsOptions { RootPath = _root, MaxSizeBytes = MaxSize }),
            new ArchiveProvider(db, NullLogger<ArchiveProvider>.Instance),
            new ProcessExecutionProvider(db, NullLogger<ProcessExecutionProvider>.Instance),
            NullLogger<ContentArchiveProcess>.Instance);
        return await process.ExecuteAsync(new ProcessContext(session, message ?? NewMessage(), Match()), CancellationToken.None);
    }

    private string[] FilesOnDisk() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : [];

    [Fact]
    public async Task SavesEml_InSubfolderOfTheArchivingFilter_AndRecordsItAsync()
    {
        var session = new FakeGmailSession();

        var status = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Succeeded, status);
        var file = Assert.Single(FilesOnDisk());
        Assert.Equal(Path.Combine(_root, "sconti", "20261001-083015_promo@negozio.it_Il tuo codice sconto.eml"), file);

        await using var db = NewContext();
        var saved = await db.ArchivedMessageContents.SingleAsync();
        Assert.Equal(MessageId, saved.MessageId);
        Assert.Equal("sconti/20261001-083015_promo@negozio.it_Il tuo codice sconto.eml", saved.RelativePath);
        Assert.Equal(new FileInfo(file).Length, saved.SizeBytes);
        Assert.Equal(64, saved.Sha256.Length);
        Assert.Equal(ProcessExecutionStatus.Succeeded, (await db.ProcessExecutions.SingleAsync()).Status);
    }

    [Fact]
    public async Task AlreadyArchived_IsSkipped_WithoutCallingGmailAsync()
    {
        await RunAsync(new FakeGmailSession());
        var second = new FakeGmailSession();

        var status = await RunAsync(second);

        Assert.Equal(ProcessExecutionStatus.Skipped, status);
        Assert.Empty(second.RawRequestedIds);
        Assert.Single(FilesOnDisk());
        await using var db = NewContext();
        Assert.Single(await db.ArchivedMessageContents.ToListAsync());
    }

    [Fact]
    public async Task OverSizeLimit_IsSkipped_WithoutWritingAsync()
    {
        var session = new FakeGmailSession { RawContent = new byte[MaxSize + 1] };

        var status = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Skipped, status);
        Assert.Empty(FilesOnDisk());
        await using var db = NewContext();
        Assert.Empty(await db.ArchivedMessageContents.ToListAsync());
        Assert.Contains("oltre il limite", (await db.ProcessExecutions.SingleAsync()).Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GmailFailure_RecordsFailed_AndLeavesNoFileAsync()
    {
        var session = new FakeGmailSession { FailOnRawId = "g1" };

        var status = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Failed, status);
        Assert.Empty(FilesOnDisk());
        await using var db = NewContext();
        Assert.Equal(ProcessExecutionStatus.Failed, (await db.ProcessExecutions.SingleAsync()).Status);
    }

    [Fact]
    public async Task DbFailureAfterFileWritten_RemovesFile_AndRecordsFailedAsync()
    {
        var failingDb = new DbContextOptionsBuilder<MailroomDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var archiveContext = new FailingArchiveDbContext(failingDb);
        await using var executionContext = NewContext();
        var process = new ContentArchiveProcess(
            Options.Create(new AttachmentsOptions { RootPath = _root, MaxSizeBytes = MaxSize }),
            new ArchiveProvider(archiveContext, NullLogger<ArchiveProvider>.Instance),
            new ProcessExecutionProvider(executionContext, NullLogger<ProcessExecutionProvider>.Instance),
            NullLogger<ContentArchiveProcess>.Instance);

        var status = await process.ExecuteAsync(new ProcessContext(new FakeGmailSession(), NewMessage(), Match()), CancellationToken.None);

        // Il file era già stato scritto e rinominato quando il DB ha fallito: non deve restare nulla
        Assert.True(archiveContext.SaveAttempted);
        Assert.Equal(ProcessExecutionStatus.Failed, status);
        Assert.Empty(FilesOnDisk());
        await using var db = NewContext();
        Assert.Equal(ProcessExecutionStatus.Failed, (await db.ProcessExecutions.SingleAsync()).Status);
        Assert.Empty(await db.ArchivedMessageContents.ToListAsync());
    }

    /// <summary>Contesto che fallisce al salvataggio dell'archivio, come un conflitto sull'indice unico di MessageId.</summary>
    private sealed class FailingArchiveDbContext(DbContextOptions<MailroomDbContext> options) : MailroomDbContext(options, NullLoggerFactory.Instance)
    {
        public bool SaveAttempted { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveAttempted = true;
            throw new DbUpdateException("Violazione dell'indice unico su MessageId");
        }
    }

    [Fact]
    public async Task MissingSubject_UsesFallbackNameAsync()
    {
        var status = await RunAsync(new FakeGmailSession(), NewMessage(subject: null));

        Assert.Equal(ProcessExecutionStatus.Succeeded, status);
        Assert.EndsWith("_promo@negozio.it_mail.eml", Assert.Single(FilesOnDisk()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reapply_WithOnlyContentArchive_ArchivesIndexedMailOfTheFilterAsync()
    {
        await using var harness = new RunTestHarness();
        await harness.AddMailboxAsync(1, backfillCompleted: true);
        await harness.AddFilterAsync(7, 1, ProcessType.AttachmentExtraction, ProcessType.ContentArchive);
        await using (var db = harness.Db.CreateDbContext())
        {
            db.Messages.Add(new Message
            {
                MailboxId = 1,
                GmailMessageId = "old-1",
                ThreadId = "t-old-1",
                SenderAddress = "fornitore@example.com",
                Subject = "Codice SCONTO10",
                ReceivedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();
        }

        var run = await harness.RunAsync(1, RunTrigger.Reapply, filterId: 7, processes: nameof(ProcessType.ContentArchive));

        Assert.Equal(RunStatus.Completed, run.Status);
        var file = Assert.Single(harness.FilesOnDisk());
        Assert.EndsWith(".eml", file, StringComparison.Ordinal);
        await using var check = harness.Db.CreateDbContext();
        Assert.Single(await check.ArchivedMessageContents.ToListAsync());
        Assert.Empty(await check.ExtractedAttachments.ToListAsync());
    }
}
