using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Runs;
using dr_mailroom_tests.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_tests.Runs;

/// <summary>Verifica il giro completo: lock (T8), backfill senza processi (D12), riapplica (D13), isolamento tra caselle (T9).</summary>
public sealed class RunOrchestratorTests : IAsyncDisposable
{
    private readonly RunTestHarness _harness = new();

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    /// <summary>Casella con storico di due mail con allegato e backfill già eseguito.</summary>
    private async Task<FakeGmailSession> MailboxAfterBackfillAsync(int mailboxId)
    {
        var session = await _harness.AddMailboxAsync(mailboxId);
        session.ProfileHistoryId = 500;
        session.Pages[string.Empty] = new MessageIdPage(["old1", "old2"], null);
        session.AttachmentParts.Add(new GmailAttachmentPart("1", "fattura.pdf", "application/pdf", 20, "att-1", null));

        var backfill = await _harness.RunAsync(mailboxId, RunTrigger.Manual);
        Assert.Equal(RunStatus.Completed, backfill.Status);
        return session;
    }

    [Fact]
    public async Task BusyMailbox_RunIsSkipped_AndNothingIsReadAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.Pages[string.Empty] = new MessageIdPage(["a"], null);
        _harness.Lock.Occupy(1);

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Skipped, run.Status);
        Assert.Equal(RunOrchestrator.MailboxBusyMessage, run.ErrorSummary);
        Assert.Empty(session.RequestedPageTokens);
    }

    [Fact]
    public async Task Backfill_CreatesNoAlertsNorFilesAsync()
    {
        await _harness.AddFilterAsync(1, null, ProcessType.Alert, ProcessType.AttachmentExtraction);

        await MailboxAfterBackfillAsync(1);

        await using var db = _harness.Db.CreateDbContext();
        Assert.Equal(2, await db.Messages.CountAsync());
        Assert.Empty(await db.Alerts.ToListAsync());
        Assert.Empty(await db.ProcessExecutions.ToListAsync());
        Assert.Empty(_harness.FilesOnDisk());
    }

    [Fact]
    public async Task IncrementalRun_AppliesFiltersToNewMessagesOnlyAsync()
    {
        await _harness.AddFilterAsync(1, null, ProcessType.Alert, ProcessType.AttachmentExtraction);
        var session = await MailboxAfterBackfillAsync(1);
        session.Changes = new HistoryChanges(["new1"], 600);

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(1, run.MessagesProcessed);
        await using var db = _harness.Db.CreateDbContext();
        var alert = await db.Alerts.Include(a => a.Message).SingleAsync();
        Assert.Equal("new1", alert.Message!.GmailMessageId);
        Assert.Single(_harness.FilesOnDisk());
    }

    [Fact]
    public async Task ManyNewMessages_AreAllProcessedByParallelConsumersAsync()
    {
        await _harness.AddFilterAsync(1, null, ProcessType.Alert, ProcessType.AttachmentExtraction);
        var session = await MailboxAfterBackfillAsync(1);
        var newIds = Enumerable.Range(1, 12).Select(i => "new" + i).ToList();
        session.Changes = new HistoryChanges(newIds, 600);

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(12, run.MessagesProcessed);
        Assert.Equal(0, run.ErrorCount);
        await using var db = _harness.Db.CreateDbContext();
        Assert.Equal(12, await db.Alerts.CountAsync());
        Assert.Equal(12, _harness.FilesOnDisk().Length);
        Assert.Equal(1, session.MaxConcurrentMetadataReads);
    }

    [Fact]
    public async Task ProducerFailure_MessagesAlreadyQueuedAreStillProcessedAsync()
    {
        await _harness.AddFilterAsync(1, null, ProcessType.Alert);
        var session = await MailboxAfterBackfillAsync(1);

        // History scaduta → scansione completa: la prima pagina porta "n1", la seconda fallisce
        session.Changes = null;
        session.Pages[string.Empty] = new MessageIdPage(["old1", "old2", "n1"], "p2");
        session.FailOnPageToken = "p2";

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Failed, run.Status);
        await using var db = _harness.Db.CreateDbContext();
        var alert = await db.Alerts.Include(a => a.Message).SingleAsync();
        Assert.Equal("n1", alert.Message!.GmailMessageId);
    }

    [Fact]
    public async Task Reapply_AttachmentsOnly_CreatesNoAlerts_AndIsIdempotentAsync()
    {
        var filterId = await _harness.AddFilterAsync(1, null, ProcessType.Alert, ProcessType.AttachmentExtraction);
        var session = await MailboxAfterBackfillAsync(1);

        var first = await _harness.RunAsync(1, RunTrigger.Reapply, filterId, nameof(ProcessType.AttachmentExtraction));
        var second = await _harness.RunAsync(1, RunTrigger.Reapply, filterId, nameof(ProcessType.AttachmentExtraction));

        Assert.Equal(RunStatus.Completed, first.Status);
        Assert.Equal(2, first.MessagesRead);
        Assert.Equal(2, first.MessagesProcessed);
        Assert.Equal(RunStatus.Completed, second.Status);

        await using var db = _harness.Db.CreateDbContext();
        Assert.Empty(await db.Alerts.ToListAsync());
        Assert.Equal(2, await db.ExtractedAttachments.CountAsync());
        Assert.Equal(2, _harness.FilesOnDisk().Length);
        Assert.Equal(2, session.DownloadedPartIds.Count);
    }

    [Fact]
    public async Task Reapply_WithoutProcessList_AppliesAllFilterProcessesAsync()
    {
        var filterId = await _harness.AddFilterAsync(1, null, ProcessType.Alert);
        await MailboxAfterBackfillAsync(1);

        await _harness.RunAsync(1, RunTrigger.Reapply, filterId);

        await using var db = _harness.Db.CreateDbContext();
        Assert.Equal(2, await db.Alerts.CountAsync());
        Assert.Empty(_harness.FilesOnDisk());
    }

    [Fact]
    public async Task ErrorOnMailboxA_DoesNotBlockMailboxBAsync()
    {
        var sessionA = await _harness.AddMailboxAsync(1);
        sessionA.Pages[string.Empty] = new MessageIdPage(["a1"], null);
        sessionA.FailOnMetadataId = "a1";
        var sessionB = await _harness.AddMailboxAsync(2);
        sessionB.Pages[string.Empty] = new MessageIdPage(["b1", "b2"], null);

        await _harness.Services.GetRequiredService<ScheduledRunner>().RunAllAsync(CancellationToken.None);

        await using var db = _harness.Db.CreateDbContext();
        var runs = await db.Runs.OrderBy(r => r.MailboxId).ToListAsync();
        Assert.Equal([RunStatus.Failed, RunStatus.Completed], runs.Select(r => r.Status));
        Assert.All(runs, r => Assert.Equal(RunTrigger.Scheduled, r.Trigger));
        Assert.Equal(2, await db.Messages.CountAsync(m => m.MailboxId == 2));
        var failed = await db.Mailboxes.SingleAsync(m => m.Id == 1);
        var healthy = await db.Mailboxes.SingleAsync(m => m.Id == 2);
        Assert.NotNull(failed.LastError);
        Assert.Equal(runs[0].Id, failed.LastErrorRunId);
        Assert.Null(healthy.LastError);
        Assert.Null(healthy.LastErrorRunId);
    }

    [Fact]
    public async Task CompletedRun_ClearsPreviousErrorAndItsRunAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.Pages[string.Empty] = new MessageIdPage(["a1"], null);
        session.FailOnMetadataId = "a1";
        await _harness.RunAsync(1, RunTrigger.Manual);

        session.FailOnMetadataId = null;
        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Completed, run.Status);
        await using var db = _harness.Db.CreateDbContext();
        var mailbox = await db.Mailboxes.SingleAsync();
        Assert.Null(mailbox.LastError);
        Assert.Null(mailbox.LastErrorRunId);
    }

    [Fact]
    public async Task RevokedToken_MarksMailboxTokenRevokedAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.FailEverythingWith = new TokenRevokedException(new InvalidOperationException("invalid_grant"));

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Failed, run.Status);
        await using var db = _harness.Db.CreateDbContext();
        var mailbox = await db.Mailboxes.SingleAsync();
        Assert.Equal(MailboxStatus.TokenRevoked, mailbox.Status);
        Assert.NotNull(mailbox.LastError);
    }

    [Fact]
    public async Task FailedIncremental_DoesNotPersistAdvancedHistoryIdAsync()
    {
        var session = await MailboxAfterBackfillAsync(1);
        session.Changes = new HistoryChanges(["ok", "ko"], 900);
        session.FailOnMetadataId = "ko";

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Failed, run.Status);
        await using var db = _harness.Db.CreateDbContext();
        Assert.Equal(500UL, (await db.Mailboxes.SingleAsync()).HistoryId);
    }

    /// <summary>Crea un giro ed eseguilo con il token del job indicato.</summary>
    private async Task<Run> ExecuteWithJobTokenAsync(int mailboxId, CancellationToken jobToken)
    {
        long runId;
        await using (var scope = _harness.Services.CreateAsyncScope())
        {
            runId = await scope.ServiceProvider.GetRequiredService<RunProvider>()
                .CreateRunAsync(mailboxId, RunTrigger.Manual, null, null, null, CancellationToken.None);
        }

        await using (var scope = _harness.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RunOrchestrator>().ExecuteAsync(runId, jobToken);
        }

        await using var db = _harness.Db.CreateDbContext();
        return await db.Runs.SingleAsync(r => r.Id == runId);
    }

    [Fact]
    public async Task CancelDuringIngestion_ClosesRunCancelled_AndNextRunResumesAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.Pages[string.Empty] = new MessageIdPage(["a", "b"], null);
        session.MetadataReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = _harness.RunAsync(1, RunTrigger.Manual);
        await session.MetadataReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        long runId;
        await using (var db = _harness.Db.CreateDbContext())
        {
            runId = await db.Runs.Where(r => r.Status == RunStatus.Running).Select(r => r.Id).SingleAsync();
        }

        Assert.True(_harness.Services.GetRequiredService<RunCancellationRegistry>().TryCancel(runId));
        var cancelled = await running.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(RunStatus.Cancelled, cancelled.Status);
        Assert.Equal(RunOrchestrator.RunCancelledMessage, cancelled.ErrorSummary);
        Assert.NotNull(cancelled.EndedAtUtc);

        // Lock rilasciato e registro ripulito: il giro successivo riprende il backfill e lo completa
        session.MetadataReached = null;
        var next = await _harness.RunAsync(1, RunTrigger.Manual);
        Assert.Equal(RunStatus.Completed, next.Status);
        Assert.False(_harness.Services.GetRequiredService<RunCancellationRegistry>().TryCancel(runId));
        await using var check = _harness.Db.CreateDbContext();
        Assert.Equal(2, await check.Messages.CountAsync());
        Assert.Null((await check.Mailboxes.SingleAsync()).LastError);
    }

    [Fact]
    public async Task CancelledJobToken_ClosesRunCancelledAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.Pages[string.Empty] = new MessageIdPage(["a"], null);

        var run = await ExecuteWithJobTokenAsync(1, new CancellationToken(canceled: true));

        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Equal(RunOrchestrator.RunCancelledMessage, run.ErrorSummary);
        Assert.Empty(session.RequestedPageTokens);
    }

    [Fact]
    public async Task ServiceStopping_ClosesRunCancelledWithStopReasonAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.Pages[string.Empty] = new MessageIdPage(["a"], null);
        _harness.Services.GetRequiredService<RunCancellationRegistry>().CancelAll();

        var run = await ExecuteWithJobTokenAsync(1, CancellationToken.None);

        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Equal(RunOrchestrator.ServiceStoppingMessage, run.ErrorSummary);
    }

    [Fact]
    public async Task PausedMailbox_RunIsSkipped_AndNothingIsReadAsync()
    {
        var session = await _harness.AddMailboxAsync(1);
        session.Pages[string.Empty] = new MessageIdPage(["a"], null);
        await using (var db = _harness.Db.CreateDbContext())
        {
            (await db.Mailboxes.AsTracking().SingleAsync()).Status = MailboxStatus.Disabled;
            await db.SaveChangesAsync();
        }

        var run = await _harness.RunAsync(1, RunTrigger.Manual);

        Assert.Equal(RunStatus.Skipped, run.Status);
        Assert.Equal(RunOrchestrator.MailboxPausedMessage, run.ErrorSummary);
        Assert.Empty(session.RequestedPageTokens);
    }
}
