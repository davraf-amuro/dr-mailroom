using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>Panoramica mittenti e mail per anno: aggregazione per indirizzo, prima/ultima mail, filtri e paginazione.</summary>
public class StatsProviderTests
{
    private readonly InMemoryMailroomDb _db = new();
    private int _sequence;

    private Message Mail(string sender, DateTime receivedAtUtc, string? subject, string? name = null, int mailboxId = 1) => new()
    {
        MailboxId = mailboxId,
        GmailMessageId = $"g-{++_sequence}",
        ThreadId = $"t-{_sequence}",
        SenderAddress = sender,
        SenderName = name,
        Subject = subject,
        ReceivedAtUtc = receivedAtUtc,
    };

    private static DateTime At(int year, int month, int day) => new(year, month, day, 9, 0, 0, DateTimeKind.Utc);

    private async Task SeedAsync(params Message[] messages)
    {
        await using var seed = _db.CreateDbContext();
        seed.Mailboxes.Add(new Mailbox { Id = 1, EmailAddress = "uno@example.com" });
        seed.Mailboxes.Add(new Mailbox { Id = 2, EmailAddress = "due@example.com" });
        seed.Messages.AddRange(messages);
        await seed.SaveChangesAsync();
    }

    private async Task<SenderOverviewPage> OverviewAsync(
        int? year = null,
        string? search = null,
        SenderOverviewSortBy sortBy = SenderOverviewSortBy.MailCount,
        SortDirection sortDir = SortDirection.Desc,
        int page = 1,
        int pageSize = 25)
    {
        await using var context = _db.CreateDbContext();
        var provider = new StatsProvider(context, NullLogger<StatsProvider>.Instance);
        return await provider.GetSenderOverviewAsync(1, year, search, sortBy, sortDir, page, pageSize, CancellationToken.None);
    }

    [Fact]
    public async Task Overview_AggregatesByAddress_WithOldestAndNewestMailAsync()
    {
        await SeedAsync(
            Mail("a@x.it", At(2024, 3, 1), "vecchia", "Vecchio Nome"),
            Mail("a@x.it", At(2026, 5, 1), "recente", "Nome Nuovo"),
            Mail("a@x.it", At(2025, 1, 1), "mezzo", null),
            Mail("b@x.it", At(2025, 7, 1), "unica"),
            Mail("a@x.it", At(2026, 9, 1), "altra casella", mailboxId: 2));

        var result = await OverviewAsync();

        Assert.Equal(2, result.TotalCount);
        var first = result.Items[0];
        Assert.Equal("a@x.it", first.SenderAddress);
        Assert.Equal(3, first.MailCount);
        Assert.Equal(At(2024, 3, 1), first.FirstReceivedAtUtc);
        Assert.Equal("vecchia", first.FirstSubject);
        Assert.Equal(At(2026, 5, 1), first.LastReceivedAtUtc);
        Assert.Equal("recente", first.LastSubject);
        Assert.Equal("Nome Nuovo", first.SenderName);
        Assert.Equal("b@x.it", result.Items[1].SenderAddress);
    }

    [Fact]
    public async Task Overview_SameReceivedAt_TieBrokenOnIdAsync()
    {
        var when = At(2026, 1, 1);
        await SeedAsync(Mail("a@x.it", when, "prima inserita"), Mail("a@x.it", when, "seconda inserita"));

        var row = Assert.Single((await OverviewAsync()).Items);

        Assert.Equal("prima inserita", row.FirstSubject);
        Assert.Equal("seconda inserita", row.LastSubject);
    }

    [Fact]
    public async Task Overview_Year_LimitsCountsAndSubjectsToThatYearAsync()
    {
        await SeedAsync(
            Mail("a@x.it", At(2024, 12, 31), "2024"),
            Mail("a@x.it", At(2025, 1, 1), "inizio 2025"),
            Mail("a@x.it", At(2025, 12, 31), "fine 2025"),
            Mail("a@x.it", At(2026, 1, 1), "2026"),
            Mail("b@x.it", At(2026, 2, 1), "solo 2026"));

        var result = await OverviewAsync(year: 2025);

        var row = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, row.MailCount);
        Assert.Equal("inizio 2025", row.FirstSubject);
        Assert.Equal("fine 2025", row.LastSubject);
    }

    [Fact]
    public async Task Overview_SearchOnAddress_AndPagingWithTotalAsync()
    {
        await SeedAsync(
            Mail("fatture@uno.it", At(2026, 1, 1), "f1"),
            Mail("fatture@due.it", At(2026, 1, 2), "f2"),
            Mail("fatture@due.it", At(2026, 1, 3), "f3"),
            Mail("news@tre.it", At(2026, 1, 4), "n"));

        var page1 = await OverviewAsync(search: "fatture", pageSize: 1);
        var page2 = await OverviewAsync(search: "fatture", pageSize: 1, page: 2);

        Assert.Equal(2, page1.TotalCount);
        Assert.Equal("fatture@due.it", Assert.Single(page1.Items).SenderAddress);
        Assert.Equal("fatture@uno.it", Assert.Single(page2.Items).SenderAddress);
    }

    [Theory]
    [InlineData(SenderOverviewSortBy.SenderAddress, SortDirection.Asc, new[] { "a@x.it", "b@x.it", "c@x.it" })]
    [InlineData(SenderOverviewSortBy.LastReceivedAt, SortDirection.Desc, new[] { "c@x.it", "a@x.it", "b@x.it" })]
    [InlineData(SenderOverviewSortBy.FirstReceivedAt, SortDirection.Asc, new[] { "b@x.it", "a@x.it", "c@x.it" })]
    [InlineData(SenderOverviewSortBy.MailCount, SortDirection.Asc, new[] { "b@x.it", "c@x.it", "a@x.it" })]
    public async Task Overview_SortsInRequestedOrder_TiesByAddressAsync(SenderOverviewSortBy sortBy, SortDirection sortDir, string[] expected)
    {
        await SeedAsync(
            Mail("a@x.it", At(2024, 6, 1), "a1"),
            Mail("a@x.it", At(2025, 6, 1), "a2"),
            Mail("b@x.it", At(2023, 1, 1), "b"),
            Mail("c@x.it", At(2026, 1, 1), "c"));

        var result = await OverviewAsync(sortBy: sortBy, sortDir: sortDir);

        Assert.Equal(expected, result.Items.Select(i => i.SenderAddress));
    }

    [Fact]
    public async Task Overview_NoMessages_ReturnsEmptyPageAsync()
    {
        await SeedAsync();

        var result = await OverviewAsync();

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task YearStat_GroupsByYearDescending_SenderMatchedExactlyAsync()
    {
        await SeedAsync(
            Mail("a@x.it", At(2024, 3, 1), "1"),
            Mail("a@x.it", At(2026, 5, 1), "2"),
            Mail("a@x.it", At(2026, 6, 1), "3"),
            Mail("aa@x.it", At(2025, 1, 1), "contiene a@x.it ma non è lui"),
            Mail("a@x.it", At(2025, 1, 1), "altra casella", mailboxId: 2));

        await using var context = _db.CreateDbContext();
        var provider = new StatsProvider(context, NullLogger<StatsProvider>.Instance);

        var sender = await provider.GetYearStatAsync(1, "a@x.it", CancellationToken.None);
        var all = await provider.GetYearStatAsync(1, null, CancellationToken.None);

        Assert.Equal([new YearStat(2026, 2), new YearStat(2024, 1)], sender);
        Assert.Equal([new YearStat(2026, 2), new YearStat(2025, 1), new YearStat(2024, 1)], all);
    }

    [Fact]
    public void OverviewPageQuery_TranslatesToSqlServer_WithoutClientEvaluation()
    {
        // Solo generazione della query: nessuna connessione aperta
        var options = new DbContextOptionsBuilder<MailroomDbContext>()
            .UseSqlServer("Server=localhost;Database=query-only;Trusted_Connection=True")
            .Options;
        using var context = new MailroomDbContext(options, NullLoggerFactory.Instance);
        var provider = new StatsProvider(context, NullLogger<StatsProvider>.Instance);

        var sql = provider
            .BuildSenderOverviewPageQuery(1, 2025, "fatture", SenderOverviewSortBy.LastReceivedAt, SortDirection.Desc, 2, 25)
            .ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Subject]", sql, StringComparison.Ordinal);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
    }
}
