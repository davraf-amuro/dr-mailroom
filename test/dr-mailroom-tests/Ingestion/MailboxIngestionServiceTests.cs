using System.Threading.Channels;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Ingestion;
using dr_mailroom_tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Ingestion;

/// <summary>Verifica backfill riprendibile, incrementale, idempotenza e tenuta del checkpoint (D2, T3, T4).</summary>
public class MailboxIngestionServiceTests
{
    private const int MailboxId = 1;
    private const long RunId = 10;

    private readonly InMemoryMailroomDb _db = new();

    /// <summary>Nuovo contesto sullo stesso DB InMemory del test: ogni giro usa un contesto pulito, come in produzione.</summary>
    private MailroomDbContext NewContext() => _db.CreateDbContext();

    /// <summary>Crea casella collegata e un giro in corso.</summary>
    private async Task SeedAsync(bool backfillCompleted = false, ulong? historyId = null)
    {
        await using var db = NewContext();
        db.Mailboxes.Add(new Mailbox
        {
            Id = MailboxId,
            EmailAddress = "me@example.com",
            EncryptedRefreshToken = "token",
            BackfillCompleted = backfillCompleted,
            HistoryId = historyId,
        });
        db.Runs.Add(new Run { Id = RunId, MailboxId = MailboxId, Trigger = RunTrigger.Manual, StartedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>Esegue un giro di ingestione con un contesto nuovo; newMessages riceve gli id accodati.</summary>
    private async Task<IngestionResult> IngestAsync(FakeGmailSession session, ChannelWriter<long>? newMessages = null)
    {
        await using var db = NewContext();
        var service = new MailboxIngestionService(
            new MailboxProvider(db, _db, NullLogger<MailboxProvider>.Instance),
            new RunProvider(db, _db, NullLogger<RunProvider>.Instance),
            new MessageProvider(db, NullLogger<MessageProvider>.Instance),
            NullLogger<MailboxIngestionService>.Instance);
        return await service.IngestAsync(MailboxId, RunId, session, newMessages, CancellationToken.None);
    }

    /// <summary>Stato salvato della casella.</summary>
    private async Task<Mailbox> LoadMailboxAsync()
    {
        await using var db = NewContext();
        return await db.Mailboxes.SingleAsync(m => m.Id == MailboxId);
    }

    /// <summary>Id Gmail salvati, ordinati.</summary>
    private async Task<List<string>> LoadGmailIdsAsync()
    {
        await using var db = NewContext();
        return await db.Messages.OrderBy(m => m.GmailMessageId).Select(m => m.GmailMessageId).ToListAsync();
    }

    /// <summary>Casella con due pagine di backfill: a,b poi c.</summary>
    private static FakeGmailSession TwoPageMailbox()
    {
        var session = new FakeGmailSession { ProfileHistoryId = 500 };
        session.Pages[string.Empty] = new MessageIdPage(["a", "b"], "p2");
        session.Pages["p2"] = new MessageIdPage(["c"], null);
        return session;
    }

    [Fact]
    public async Task Backfill_IndexesAllPages_SavesHistoryIdCapturedAtStartAsync()
    {
        await SeedAsync();

        var result = await IngestAsync(TwoPageMailbox());

        var mailbox = await LoadMailboxAsync();
        Assert.True(result.WasBackfill);
        Assert.Equal(3, result.MessagesRead);
        Assert.Equal(["a", "b", "c"], await LoadGmailIdsAsync());
        Assert.True(mailbox.BackfillCompleted);
        Assert.Null(mailbox.BackfillPageToken);
        Assert.Equal(500UL, mailbox.HistoryId);
    }

    [Fact]
    public async Task Backfill_InterruptedMidway_ResumesFromSavedPageAsync()
    {
        await SeedAsync();
        var broken = TwoPageMailbox();
        broken.FailOnPageToken = "p2";

        await Assert.ThrowsAsync<HttpRequestException>(() => IngestAsync(broken));

        var afterCrash = await LoadMailboxAsync();
        Assert.False(afterCrash.BackfillCompleted);
        Assert.Equal("p2", afterCrash.BackfillPageToken);
        Assert.Equal(["a", "b"], await LoadGmailIdsAsync());

        var healthy = TwoPageMailbox();
        healthy.ProfileHistoryId = 999;
        await IngestAsync(healthy);

        Assert.Equal(["p2"], healthy.RequestedPageTokens);
        Assert.Equal(["a", "b", "c"], await LoadGmailIdsAsync());
        var completed = await LoadMailboxAsync();
        Assert.True(completed.BackfillCompleted);
        Assert.Equal(500UL, completed.HistoryId);
    }

    [Fact]
    public async Task SecondRun_IsIncremental_AndAdvancesHistoryIdAsync()
    {
        await SeedAsync();
        await IngestAsync(TwoPageMailbox());

        var session = TwoPageMailbox();
        session.Changes = new HistoryChanges(["d"], 600);
        var result = await IngestAsync(session);

        Assert.False(result.WasBackfill);
        Assert.Equal([500UL], session.RequestedHistoryIds);
        Assert.Empty(session.RequestedPageTokens);
        Assert.Single(result.InsertedMessageIds);
        Assert.Equal(600UL, (await LoadMailboxAsync()).HistoryId);
    }

    [Fact]
    public async Task DoubleIngestion_DoesNotDuplicateMessagesAsync()
    {
        await SeedAsync(backfillCompleted: true, historyId: 500);
        var session = new FakeGmailSession { Changes = new HistoryChanges(["x", "y", "x"], 510) };

        var first = await IngestAsync(session);
        session.Changes = new HistoryChanges(["x", "y", "z"], 520);
        var second = await IngestAsync(session);

        Assert.Equal(2, first.InsertedMessageIds.Count);
        Assert.Single(second.InsertedMessageIds);
        Assert.Equal(["x", "y", "z"], await LoadGmailIdsAsync());
    }

    [Fact]
    public async Task IncrementalError_LeavesHistoryIdUnchanged_AndInsertsNothingAsync()
    {
        await SeedAsync(backfillCompleted: true, historyId: 500);
        var session = new FakeGmailSession { Changes = new HistoryChanges(["ok", "ko"], 600), FailOnMetadataId = "ko" };

        await Assert.ThrowsAsync<HttpRequestException>(() => IngestAsync(session));

        Assert.Equal(500UL, (await LoadMailboxAsync()).HistoryId);
        Assert.Empty(await LoadGmailIdsAsync());
    }

    [Fact]
    public async Task ExpiredHistory_FullScan_InsertsOnlyMissing_AndResetsHistoryIdAsync()
    {
        await SeedAsync();
        await IngestAsync(TwoPageMailbox());

        var session = TwoPageMailbox();
        session.Changes = null;
        session.ProfileHistoryId = 900;
        session.Pages["p2"] = new MessageIdPage(["c", "new"], null);
        var result = await IngestAsync(session);

        Assert.False(result.WasBackfill);
        Assert.Single(result.InsertedMessageIds);
        Assert.Equal(["a", "b", "c", "new"], await LoadGmailIdsAsync());
        Assert.Equal(900UL, (await LoadMailboxAsync()).HistoryId);
    }

    [Fact]
    public async Task Ingestion_UpdatesRunMessagesReadAsync()
    {
        await SeedAsync();

        await IngestAsync(TwoPageMailbox());

        await using var db = NewContext();
        Assert.Equal(3, (await db.Runs.SingleAsync()).MessagesRead);
    }

    [Fact]
    public async Task Ingestion_ReadsMetadataOneAtATimeAsync()
    {
        await SeedAsync();
        var session = TwoPageMailbox();

        await IngestAsync(session);

        // Produttore della coda: nessuna chiamata concorrente verso Gmail
        Assert.Equal(1, session.MaxConcurrentMetadataReads);
    }

    [Fact]
    public async Task Backfill_EnqueuesNothingAsync()
    {
        await SeedAsync();
        var queue = Channel.CreateUnbounded<long>();

        await IngestAsync(TwoPageMailbox(), queue.Writer);

        Assert.Equal(0, queue.Reader.Count);
    }

    [Fact]
    public async Task Incremental_EnqueuesSavedMessagesInOrderAsync()
    {
        await SeedAsync();
        await IngestAsync(TwoPageMailbox());
        var session = TwoPageMailbox();
        session.Changes = new HistoryChanges(["c", "n1", "n2"], 1100);
        var queue = Channel.CreateUnbounded<long>();

        var result = await IngestAsync(session, queue.Writer);
        queue.Writer.Complete();

        // "c" esiste già: in coda vanno solo i messaggi salvati, nell'ordine di inserimento
        var enqueued = await queue.Reader.ReadAllAsync().ToListAsync();
        Assert.Equal(result.InsertedMessageIds, enqueued);
        Assert.Equal(2, enqueued.Count);
    }
}
