using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>Database InMemory isolato per test, con factory di contesti come in produzione.</summary>
public sealed class InMemoryMailroomDb : IDbContextFactory<MailroomDbContext>
{
    private readonly DbContextOptions<MailroomDbContext> _options =
        new DbContextOptionsBuilder<MailroomDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    /// <summary>Nuovo contesto sullo stesso database.</summary>
    public MailroomDbContext CreateDbContext() => new(_options, NullLoggerFactory.Instance);
}
