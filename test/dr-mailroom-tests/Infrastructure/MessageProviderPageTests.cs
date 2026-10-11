using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>Griglia delle mail di un mittente: isolamento per casella e mittente, ordine per data con spareggio, ricerca, paginazione.</summary>
public class MessageProviderPageTests
{
    private readonly InMemoryMailroomDb _db = new();
    private int _sequence;

    private Message Mail(string sender, DateTime receivedAtUtc, string? subject, int mailboxId = 1) => new()
    {
        MailboxId = mailboxId,
        GmailMessageId = $"g-{++_sequence}",
        ThreadId = $"t-{_sequence}",
        SenderAddress = sender,
        Subject = subject,
        ReceivedAtUtc = receivedAtUtc,
        Labels = "INBOX,CATEGORY_PROMOTIONS",
        HasAttachments = _sequence % 2 == 0,
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

    private async Task<(List<Message> Items, int TotalCount)> PageAsync(
        string sender = "a@x.it",
        string? search = null,
        SortDirection sortDir = SortDirection.Desc,
        int page = 1,
        int pageSize = 25)
    {
        await using var context = _db.CreateDbContext();
        var provider = new MessageProvider(context, NullLogger<MessageProvider>.Instance);
        var filter = new SenderMessageFilter { MailboxId = 1, SenderAddress = sender, Search = search };
        return await provider.GetSenderMessagesPageAsync(filter, sortDir, page, pageSize, m => m, CancellationToken.None);
    }

    [Fact]
    public async Task Page_OnlySenderOfMailbox_NewestFirstAsync()
    {
        await SeedAsync(
            Mail("a@x.it", At(2024, 1, 1), "vecchia"),
            Mail("a@x.it", At(2026, 1, 1), "recente"),
            Mail("b@x.it", At(2025, 1, 1), "altro mittente"),
            Mail("a@x.it", At(2025, 6, 1), "altra casella", mailboxId: 2),
            Mail("ma-a@x.it", At(2025, 6, 1), "mittente che contiene l'indirizzo"));

        var (items, total) = await PageAsync();

        Assert.Equal(2, total);
        Assert.Equal(["recente", "vecchia"], items.Select(m => m.Subject));
    }

    [Fact]
    public async Task Page_AscendingAndTieBrokenOnIdAsync()
    {
        var when = At(2026, 1, 1);
        await SeedAsync(Mail("a@x.it", when, "prima"), Mail("a@x.it", when, "seconda"), Mail("a@x.it", At(2020, 1, 1), "antica"));

        var asc = await PageAsync(sortDir: SortDirection.Asc);
        var desc = await PageAsync();

        Assert.Equal(["antica", "prima", "seconda"], asc.Items.Select(m => m.Subject));
        Assert.Equal(["seconda", "prima", "antica"], desc.Items.Select(m => m.Subject));
    }

    [Fact]
    public async Task Page_SearchInSubject_SkipsNullSubjectsAsync()
    {
        await SeedAsync(
            Mail("a@x.it", At(2026, 1, 1), "Promozione autunno"),
            Mail("a@x.it", At(2026, 2, 1), "Fattura n. 12"),
            Mail("a@x.it", At(2026, 3, 1), null));

        var (items, total) = await PageAsync(search: "Promozione");

        Assert.Equal(1, total);
        Assert.Equal("Promozione autunno", Assert.Single(items).Subject);
    }

    [Fact]
    public async Task Page_SecondPage_KeepsTotalAsync()
    {
        await SeedAsync(
            Mail("a@x.it", At(2026, 1, 1), "1"),
            Mail("a@x.it", At(2026, 1, 2), "2"),
            Mail("a@x.it", At(2026, 1, 3), "3"));

        var (items, total) = await PageAsync(page: 2, pageSize: 2);

        Assert.Equal(3, total);
        Assert.Equal("1", Assert.Single(items).Subject);
    }

    [Fact]
    public async Task Page_NoMail_ReturnsEmptyAsync()
    {
        await SeedAsync(Mail("b@x.it", At(2026, 1, 1), "altro"));

        var (items, total) = await PageAsync();

        Assert.Equal(0, total);
        Assert.Empty(items);
    }
}
