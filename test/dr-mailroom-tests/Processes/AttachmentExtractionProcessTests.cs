using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Processes;
using dr_mailroom_tests.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dr_mailroom_tests.Processes;

/// <summary>Verifica l'estrazione allegati: nomi sicuri, nessuna uscita dalla root, idempotenza e ripresa dopo crash (T10).</summary>
public sealed class AttachmentExtractionProcessTests : IDisposable
{
    private const long MessageId = 100;
    private const long MaxSize = 1_000;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dr-mailroom-tests", Guid.NewGuid().ToString("N"));
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>Rimuove la cartella temporanea del test.</summary>
    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private MailroomDbContext NewContext() => new(
        new DbContextOptionsBuilder<MailroomDbContext>().UseInMemoryDatabase(_databaseName).Options,
        NullLoggerFactory.Instance);

    private static Message NewMessage() => new()
    {
        Id = MessageId,
        MailboxId = 1,
        GmailMessageId = "g1",
        SenderAddress = "fatture@fornitore.it",
        ReceivedAtUtc = new DateTime(2026, 10, 1, 8, 30, 15, DateTimeKind.Utc),
    };

    private static FilterMatchResult MatchWithSubfolder(string? subfolder)
    {
        var filter = new Filter
        {
            Id = 1,
            SenderAddress = "fatture@fornitore.it",
            AttachmentSubfolder = subfolder,
            Processes = [new FilterProcess { FilterId = 1, ProcessType = ProcessType.AttachmentExtraction }],
        };
        return new FilterMatchResult([filter], new HashSet<ProcessType> { ProcessType.AttachmentExtraction });
    }

    private static GmailAttachmentPart Part(string partId, string fileName, long size = 20) =>
        new(partId, fileName, "application/pdf", size, "att-" + partId, null);

    /// <summary>Esegue il processo con un contesto nuovo, come farebbe un giro.</summary>
    private async Task<ProcessExecutionStatus> RunAsync(FakeGmailSession session, string? subfolder = "fornitore")
    {
        await using var db = NewContext();
        var process = new AttachmentExtractionProcess(
            Options.Create(new AttachmentsOptions { RootPath = _root, MaxSizeBytes = MaxSize }),
            new AttachmentProvider(db, NullLogger<AttachmentProvider>.Instance),
            new ProcessExecutionProvider(db, NullLogger<ProcessExecutionProvider>.Instance),
            NullLogger<AttachmentExtractionProcess>.Instance);
        return await process.ExecuteAsync(new ProcessContext(session, NewMessage(), MatchWithSubfolder(subfolder)), CancellationToken.None);
    }

    private string[] FilesOnDisk() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : [];

    private async Task<ProcessExecution> LoadExecutionAsync()
    {
        await using var db = NewContext();
        return await db.ProcessExecutions.SingleAsync();
    }

    [Fact]
    public async Task SavesAttachment_WithDeterministicName_InFilterSubfolderAsync()
    {
        var session = new FakeGmailSession();
        session.AttachmentParts.Add(Part("1", "Fattura 42.pdf"));

        var status = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Succeeded, status);
        var file = Assert.Single(FilesOnDisk());
        Assert.Equal(Path.Combine(_root, "fornitore", "20261001-083015_fatture@fornitore.it_Fattura 42.pdf"), file);

        await using var db = NewContext();
        var saved = await db.ExtractedAttachments.SingleAsync();
        Assert.Equal("fornitore/20261001-083015_fatture@fornitore.it_Fattura 42.pdf", saved.RelativePath);
        Assert.Equal(64, saved.Sha256.Length);
        Assert.Equal(ProcessExecutionStatus.Succeeded, (await LoadExecutionAsync()).Status);
    }

    [Fact]
    public async Task PathTraversalInFileName_StaysInsideRootAsync()
    {
        var session = new FakeGmailSession();
        session.AttachmentParts.Add(Part("1", @"..\..\x.exe"));
        session.AttachmentParts.Add(Part("2", "../../etc/passwd"));

        await RunAsync(session);

        var rootFull = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        Assert.Equal(2, FilesOnDisk().Length);
        Assert.All(FilesOnDisk(), f => Assert.StartsWith(rootFull, Path.GetFullPath(f)));
        Assert.Contains(FilesOnDisk(), f => f.EndsWith("_x.exe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubfolderEscapingRoot_FailsWithoutWritingAsync()
    {
        var session = new FakeGmailSession();
        session.AttachmentParts.Add(Part("1", "a.pdf"));

        var status = await RunAsync(session, subfolder: "../../fuori");

        Assert.Equal(ProcessExecutionStatus.Failed, status);
        Assert.Empty(FilesOnDisk());
        Assert.False(Directory.Exists(Path.GetFullPath(Path.Combine(_root, "../../fuori"))));
    }

    [Fact]
    public async Task Rerun_WritesNoNewFilesAsync()
    {
        var session = new FakeGmailSession();
        session.AttachmentParts.Add(Part("1", "a.pdf"));
        session.AttachmentParts.Add(Part("2", "b.pdf"));

        await RunAsync(session);
        await RunAsync(session);

        Assert.Equal(2, FilesOnDisk().Length);
        Assert.Equal(["1", "2"], session.DownloadedPartIds);
    }

    [Fact]
    public async Task CrashAfterFirstAttachment_ResumeWritesOnlyMissingAsync()
    {
        var session = new FakeGmailSession { FailOnDownloadPartId = "2" };
        session.AttachmentParts.Add(Part("1", "a.pdf"));
        session.AttachmentParts.Add(Part("2", "b.pdf"));

        var first = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Failed, first);
        Assert.Single(FilesOnDisk());

        session.FailOnDownloadPartId = null;
        var second = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Succeeded, second);
        Assert.Equal(["1", "2"], session.DownloadedPartIds);
        Assert.Equal(2, FilesOnDisk().Length);
        Assert.DoesNotContain(FilesOnDisk(), f => Path.GetFileName(f).StartsWith(AttachmentExtractionProcess.TempFilePrefix, StringComparison.Ordinal));
        Assert.Equal(ProcessExecutionStatus.Succeeded, (await LoadExecutionAsync()).Status);
    }

    [Fact]
    public async Task OversizedAttachment_IsSkippedWithReasonAsync()
    {
        var session = new FakeGmailSession();
        session.AttachmentParts.Add(Part("1", "enorme.zip", size: MaxSize + 1));

        var status = await RunAsync(session);

        Assert.Equal(ProcessExecutionStatus.Skipped, status);
        Assert.Empty(FilesOnDisk());
        Assert.Contains("enorme.zip", (await LoadExecutionAsync()).Error);
    }

    [Fact]
    public async Task SameFileName_GetsNumericSuffixAsync()
    {
        var session = new FakeGmailSession();
        session.AttachmentParts.Add(Part("1", "doc.pdf"));
        session.AttachmentParts.Add(Part("2", "doc.pdf"));

        await RunAsync(session);

        var names = FilesOnDisk().Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            ["20261001-083015_fatture@fornitore.it_doc.pdf", "20261001-083015_fatture@fornitore.it_doc_1.pdf"],
            names);
    }

    [Theory]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("a<b>c?.pdf", "a_b_c_.pdf")]
    [InlineData("...", SafeFileName.FallbackName)]
    public void SafeFileName_SanitizesProblematicNames(string original, string expectedTail)
    {
        var name = SafeFileName.Build(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "a@b.c", original);

        Assert.Equal("20260102-030405_a@b.c_" + expectedTail, name);
    }
}
