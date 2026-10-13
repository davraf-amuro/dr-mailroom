using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>
/// Purga delle cestinate: ExecuteDeleteAsync non gira su InMemory, quindi si verifica la query su SQL Server
/// (solo generazione, nessuna connessione): tocca solo le righe con TrashedAtUtc valorizzato e più vecchio della soglia.
/// </summary>
public class PurgeTrashedQueryTests
{
    [Fact]
    public void TrashedBefore_TranslatesToSqlServer_FilteringOnTrashedAtUtc()
    {
        var options = new DbContextOptionsBuilder<MailroomDbContext>()
            .UseSqlServer("Server=localhost;Database=query-only;Trusted_Connection=True")
            .Options;
        using var context = new MailroomDbContext(options, NullLoggerFactory.Instance);
        var provider = new MessageProvider(context, NullLogger<MessageProvider>.Instance);

        var sql = provider.TrashedBefore(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).ToQueryString();

        Assert.Contains("[TrashedAtUtc] IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("[TrashedAtUtc] <", sql, StringComparison.Ordinal);
    }
}
