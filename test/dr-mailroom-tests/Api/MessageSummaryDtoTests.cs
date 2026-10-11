using dr_mailroom_api.Dto;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Api;

/// <summary>Proiezione del messaggio dell'anteprima filtri: posta inviata compresa (D20).</summary>
public class MessageSummaryDtoTests
{
    [Fact]
    public void Projection_TranslatesToSqlServer_WithIsSent()
    {
        // Solo generazione della query: nessuna connessione aperta
        var options = new DbContextOptionsBuilder<MailroomDbContext>()
            .UseSqlServer("Server=localhost;Database=query-only;Trusted_Connection=True")
            .Options;
        using var context = new MailroomDbContext(options, NullLoggerFactory.Instance);

        var sql = context.Messages.Select(MessageSummaryDto.Projection).ToQueryString();

        Assert.Contains("[IsSent]", sql, StringComparison.Ordinal);
        Assert.Contains("[HasAttachments]", sql, StringComparison.Ordinal);
    }
}
