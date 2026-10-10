using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Api;

/// <summary>Proiezione della casella: avanzamento del backfill, giro in corso e giro dell'ultimo errore.</summary>
public class MailboxDtoTests
{
    private readonly InMemoryMailroomDb _db = new();

    private static Message NewMessage(string gmailId) => new()
    {
        GmailMessageId = gmailId,
        ThreadId = "t-" + gmailId,
        SenderAddress = "fornitore@example.com",
        ReceivedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
    };

    private static Run NewRun(RunStatus status, int messagesRead) => new()
    {
        Trigger = RunTrigger.Scheduled,
        Status = status,
        StartedAtUtc = new DateTime(2026, 10, 9, 17, 30, 0, DateTimeKind.Utc),
        MessagesRead = messagesRead,
    };

    private async Task<MailboxDto> ProjectAsync(Mailbox mailbox)
    {
        await using (var seed = _db.CreateDbContext())
        {
            seed.Mailboxes.Add(mailbox);
            await seed.SaveChangesAsync();
        }

        await using var db = _db.CreateDbContext();
        return await db.Mailboxes.Select(MailboxDto.Projection).SingleAsync();
    }

    [Fact]
    public async Task Projection_CountsIndexedMessages_AndExposesRunningRunOnlyAsync()
    {
        var mailbox = new Mailbox
        {
            EmailAddress = "a@example.com",
            BackfillPageToken = "p3",
            MessagesTotal = 48210,
            Messages = [NewMessage("a"), NewMessage("b"), NewMessage("c")],
            Runs = [NewRun(RunStatus.Failed, 0), NewRun(RunStatus.Running, 1000), NewRun(RunStatus.Cancelled, 6500)],
        };

        var dto = await ProjectAsync(mailbox);

        Assert.True(dto.BackfillInProgress);
        Assert.Equal(48210, dto.MessagesTotal);
        Assert.Equal(3, dto.MessagesIndexed);
        Assert.NotNull(dto.CurrentRun);
        Assert.Equal(1000, dto.CurrentRun.MessagesRead);
    }

    [Fact]
    public async Task Projection_WithoutRunningRun_HasNullCurrentRun_AndKeepsErrorRunAsync()
    {
        var mailbox = new Mailbox
        {
            EmailAddress = "b@example.com",
            LastError = "quota superata",
            LastErrorRunId = 3,
            Runs = [NewRun(RunStatus.Completed, 10)],
        };

        var dto = await ProjectAsync(mailbox);

        Assert.Null(dto.CurrentRun);
        Assert.Equal(0, dto.MessagesIndexed);
        Assert.Equal("quota superata", dto.LastError);
        Assert.Equal(3, dto.LastErrorRunId);
    }

    [Fact]
    public void Projection_TranslatesToSqlServer_WithoutClientEvaluation()
    {
        // Solo generazione della query: nessuna connessione aperta
        var options = new DbContextOptionsBuilder<MailroomDbContext>()
            .UseSqlServer("Server=localhost;Database=query-only;Trusted_Connection=True")
            .Options;
        using var context = new MailroomDbContext(options, NullLoggerFactory.Instance);

        var sql = context.Mailboxes.Select(MailboxDto.Projection).ToQueryString();

        Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Run]", sql, StringComparison.Ordinal);
    }
}
