using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_tests.Runs;

/// <summary>
/// Giro di cestino di un mittente (D-T1 bis): solo le mail ricevute e non ancora cestinate, a blocchi da 1.000,
/// token separato, nessun effetto su LastRunAtUtc e sul token di lettura.
/// </summary>
public sealed class TrashSenderRunTests : IAsyncDisposable
{
    private const int MailboxId = 1;
    private const string Spammer = "no-reply@spam.example";
    private const string TrashToken = "trash-token-1";

    private readonly RunTestHarness _harness = new();

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    /// <summary>Casella collegata, con o senza token del cestino.</summary>
    private async Task AddMailboxAsync(bool trashAuthorized = true)
    {
        await _harness.AddMailboxAsync(MailboxId, backfillCompleted: true);
        await using var db = _harness.Db.CreateDbContext();
        var mailbox = await db.Mailboxes.AsTracking().SingleAsync(m => m.Id == MailboxId);
        mailbox.EncryptedTrashRefreshToken = trashAuthorized ? TrashToken : null;
        await db.SaveChangesAsync();
    }

    /// <summary>Aggiunge mail indicizzate: count dal mittente indicato, con prefisso dell'id Gmail.</summary>
    private async Task AddMessagesAsync(string sender, int count, string prefix, bool isSent = false, DateTime? trashedAtUtc = null)
    {
        await using var db = _harness.Db.CreateDbContext();
        for (var i = 0; i < count; i++)
        {
            db.Messages.Add(new Message
            {
                MailboxId = MailboxId,
                GmailMessageId = $"{prefix}{i}",
                ThreadId = $"t-{prefix}{i}",
                SenderAddress = sender,
                ReceivedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                IsSent = isSent,
                TrashedAtUtc = trashedAtUtc,
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<List<Message>> MessagesAsync()
    {
        await using var db = _harness.Db.CreateDbContext();
        return await db.Messages.OrderBy(m => m.Id).ToListAsync();
    }

    private async Task<Mailbox> MailboxAsync()
    {
        await using var db = _harness.Db.CreateDbContext();
        return await db.Mailboxes.SingleAsync(m => m.Id == MailboxId);
    }

    [Fact]
    public async Task TrashSender_TrashesOnlyReceivedNotYetTrashedMailOfTheSenderAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, 3, "spam-");
        await AddMessagesAsync(Spammer, 1, "sent-", isSent: true);
        await AddMessagesAsync(Spammer, 1, "old-", trashedAtUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddMessagesAsync("amico@example.com", 2, "friend-");

        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(3, run.MessagesRead);
        Assert.Equal(3, run.MessagesProcessed);
        var batch = Assert.Single(_harness.Trasher.Batches[TrashToken]);
        Assert.Equal(["spam-0", "spam-1", "spam-2"], batch);

        var messages = await MessagesAsync();
        Assert.All(messages.Where(m => m.GmailMessageId.StartsWith("spam-", StringComparison.Ordinal)), m => Assert.NotNull(m.TrashedAtUtc));
        Assert.Null(messages.Single(m => m.GmailMessageId == "sent-0").TrashedAtUtc);
        Assert.All(messages.Where(m => m.SenderAddress == "amico@example.com"), m => Assert.Null(m.TrashedAtUtc));
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), messages.Single(m => m.GmailMessageId == "old-0").TrashedAtUtc);
    }

    [Fact]
    public async Task TrashSender_WithReceivedBefore_TrashesOnlyOlderMailAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, 4, "spam-");

        // Estremo escluso: spam-0 (08:00) e spam-1 (08:01) prima, spam-2 (08:02) esattamente al limite resta
        var limit = new DateTime(2026, 10, 1, 8, 2, 0, DateTimeKind.Utc);
        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer, trashReceivedBeforeUtc: limit);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(limit, run.TrashReceivedBeforeUtc);
        Assert.Equal(2, run.MessagesProcessed);
        Assert.Equal(["spam-0", "spam-1"], Assert.Single(_harness.Trasher.Batches[TrashToken]));
        var kept = (await MessagesAsync()).Where(m => m.TrashedAtUtc == null).Select(m => m.GmailMessageId);
        Assert.Equal(["spam-2", "spam-3"], kept);
    }

    [Fact]
    public async Task TrashSender_DoesNotTouchLastRunNorReadTokenAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, 1, "spam-");

        await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        var mailbox = await MailboxAsync();
        Assert.Null(mailbox.LastRunAtUtc);
        Assert.Equal("token-" + MailboxId, mailbox.EncryptedRefreshToken);
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
    }

    [Fact]
    public async Task TrashSender_SplitsIntoBatchesOfTheGmailLimitAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, GmailConstants.MaxBatchModifyIds + 5, "spam-");

        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal([GmailConstants.MaxBatchModifyIds, 5], _harness.Trasher.Batches[TrashToken].Select(b => b.Count));
        Assert.Equal(GmailConstants.MaxBatchModifyIds + 5, run.MessagesProcessed);
    }

    [Fact]
    public async Task TrashSender_WithoutTrashToken_FailsWithoutCallingGmailAsync()
    {
        await AddMailboxAsync(trashAuthorized: false);
        await AddMessagesAsync(Spammer, 2, "spam-");

        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal(RunOrchestrator.TrashNotAuthorizedMessage, run.ErrorSummary);
        Assert.Empty(_harness.Trasher.Batches);
        Assert.All(await MessagesAsync(), m => Assert.Null(m.TrashedAtUtc));
    }

    [Fact]
    public async Task TrashSender_RevokedTrashToken_ClearsOnlyTrashTokenAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, 2, "spam-");
        _harness.Trasher.RevokedTokens.Add(TrashToken);

        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal(RunOrchestrator.TrashTokenRevokedMessage, run.ErrorSummary);
        var mailbox = await MailboxAsync();
        Assert.Null(mailbox.EncryptedTrashRefreshToken);
        Assert.Equal("token-" + MailboxId, mailbox.EncryptedRefreshToken);
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
    }

    [Fact]
    public async Task TrashSender_FailureOnSecondBatch_KeepsFirstBatchMarkedAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, GmailConstants.MaxBatchModifyIds + 3, "spam-");
        _harness.Trasher.FailAtBatch = 1;

        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal(GmailConstants.MaxBatchModifyIds + 3, run.MessagesRead);
        Assert.Equal(GmailConstants.MaxBatchModifyIds, run.MessagesProcessed);
        Assert.Equal(GmailConstants.MaxBatchModifyIds, (await MessagesAsync()).Count(m => m.TrashedAtUtc is not null));
    }

    [Fact]
    public async Task TrashSender_CancelledBetweenBatches_StopsAndKeepsWhatIsDoneAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, (GmailConstants.MaxBatchModifyIds * 2) + 1, "spam-");

        // Primo giro della casella in un DB nuovo: Id 1. Dopo il primo blocco arriva l'annullamento
        var registry = _harness.Services.GetRequiredService<RunCancellationRegistry>();
        _harness.Trasher.AfterBatch = done =>
        {
            if (done == 1)
            {
                registry.TryCancel(1);
            }
        };

        var run = await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Single(_harness.Trasher.Batches[TrashToken]);
        Assert.Equal(GmailConstants.MaxBatchModifyIds, (await MessagesAsync()).Count(m => m.TrashedAtUtc is not null));
    }

    [Fact]
    public async Task TrashSender_RunIsExcludedFromLastConcludedRunCountAsync()
    {
        await AddMailboxAsync();
        await AddMessagesAsync(Spammer, 2, "spam-");

        await _harness.RunAsync(MailboxId, RunTrigger.TrashSender, senderAddress: Spammer);

        await using var db = _harness.Db.CreateDbContext();
        var dto = await db.Mailboxes.Select(dr_mailroom_api.Dto.MailboxDto.Projection).SingleAsync();
        Assert.Null(dto.LastRunMessagesRead);
        Assert.True(dto.TrashAuthorized);
    }
}
