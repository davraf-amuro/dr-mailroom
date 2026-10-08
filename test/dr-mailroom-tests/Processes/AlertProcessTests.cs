using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Processes;
using dr_mailroom_tests.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Processes;

/// <summary>Verifica una sola allerta per messaggio con tutti i filtri collegati (D3) e l'idempotenza.</summary>
public class AlertProcessTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    private MailroomDbContext NewContext() => new(
        new DbContextOptionsBuilder<MailroomDbContext>().UseInMemoryDatabase(_databaseName).Options,
        NullLoggerFactory.Instance);

    private static Message NewMessage() => new()
    {
        Id = 100,
        MailboxId = 1,
        GmailMessageId = "g1",
        SenderAddress = "fatture@fornitore.it",
        Subject = "Sollecito di pagamento",
        ReceivedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
    };

    private static Filter AlertFilter(int id, params ProcessType[] processes) => new()
    {
        Id = id,
        SenderAddress = "fatture@fornitore.it",
        Processes = (processes.Length == 0 ? [ProcessType.Alert] : processes)
            .Select(p => new FilterProcess { FilterId = id, ProcessType = p }).ToList(),
    };

    private static FilterMatchResult Match(params Filter[] filters) =>
        new(filters, filters.SelectMany(f => f.Processes).Select(p => p.ProcessType).ToHashSet());

    private async Task<ProcessExecutionStatus> RunAsync(FilterMatchResult match)
    {
        await using var db = NewContext();
        var process = new AlertProcess(
            new AlertProvider(db, NullLogger<AlertProvider>.Instance),
            new ProcessExecutionProvider(db, NullLogger<ProcessExecutionProvider>.Instance),
            NullLogger<AlertProcess>.Instance);
        return await process.ExecuteAsync(new ProcessContext(new FakeGmailSession(), NewMessage(), match), CancellationToken.None);
    }

    [Fact]
    public async Task TwoFiltersWithAlert_CreateOneAlert_WithTwoLinksAsync()
    {
        var status = await RunAsync(Match(AlertFilter(1), AlertFilter(2)));

        Assert.Equal(ProcessExecutionStatus.Succeeded, status);
        await using var db = NewContext();
        var alert = await db.Alerts.Include(a => a.Filters).SingleAsync();
        Assert.Equal("fatture@fornitore.it", alert.SenderAddress);
        Assert.Equal("Sollecito di pagamento", alert.Subject);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), alert.ReceivedAtUtc);
        Assert.Null(alert.ConsumedAtUtc);
        Assert.Equal([1, 2], alert.Filters.Select(f => f.FilterId).Order());
    }

    [Fact]
    public async Task Rerun_CreatesNoNewAlertAsync()
    {
        await RunAsync(Match(AlertFilter(1), AlertFilter(2)));
        await RunAsync(Match(AlertFilter(1), AlertFilter(2)));

        await using var db = NewContext();
        Assert.Single(await db.Alerts.ToListAsync());
        Assert.Equal(2, await db.AlertFilters.CountAsync());
        Assert.Single(await db.ProcessExecutions.ToListAsync());
    }

    [Fact]
    public async Task Reapply_WithNewFilter_AddsOnlyTheLinkAsync()
    {
        await RunAsync(Match(AlertFilter(1)));
        await RunAsync(Match(AlertFilter(1), AlertFilter(5)));

        await using var db = NewContext();
        var alert = await db.Alerts.Include(a => a.Filters).SingleAsync();
        Assert.Equal([1, 5], alert.Filters.Select(f => f.FilterId).Order());
    }

    [Fact]
    public async Task FilterWithoutAlertProcess_IsNotLinkedAsync()
    {
        await RunAsync(Match(AlertFilter(1), AlertFilter(2, ProcessType.AttachmentExtraction)));

        await using var db = NewContext();
        Assert.Equal([1], await db.AlertFilters.Select(f => f.FilterId).ToListAsync());
    }
}
