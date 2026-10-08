using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Infrastructure;

/// <summary>Verifica nel modello EF i vincoli che garantiscono idempotenza e coerenza dei dati (T4, T5, T10, D3, D4).</summary>
public class MailroomModelTests
{
    /// <summary>Modello di design-time: l'unico che conserva i CHECK constraint. Nessuna connessione aperta.</summary>
    private static IModel BuildDesignTimeModel()
    {
        var options = new DbContextOptionsBuilder<MailroomDbContext>()
            .UseSqlServer("Server=localhost;Database=model-only;Trusted_Connection=True")
            .Options;
        using var context = new MailroomDbContext(options, NullLoggerFactory.Instance);
        return context.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>True se l'entità ha un indice UNIQUE esattamente sulle colonne indicate, nell'ordine dato.</summary>
    private static bool HasUniqueIndex<TEntity>(IModel model, params string[] properties)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        return entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(properties));
    }

    [Theory]
    [InlineData(typeof(Mailbox), new[] { "EmailAddress" })]
    [InlineData(typeof(Message), new[] { "MailboxId", "GmailMessageId" })]
    [InlineData(typeof(Alert), new[] { "MessageId" })]
    [InlineData(typeof(ProcessExecution), new[] { "MessageId", "ProcessType" })]
    [InlineData(typeof(ExtractedAttachment), new[] { "MessageId", "PartId" })]
    public void UniqueIndexes_ArePresent(Type entityType, string[] properties)
    {
        var model = BuildDesignTimeModel();
        var method = typeof(MailroomModelTests)
            .GetMethod(nameof(HasUniqueIndex), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        var found = (bool)method.Invoke(null, [model, properties])!;

        Assert.True(found, $"Indice UNIQUE mancante su {entityType.Name}({string.Join(", ", properties)})");
    }

    [Fact]
    public void Filter_HasSubjectCheckConstraint()
    {
        var model = BuildDesignTimeModel();
        var filter = model.FindEntityType(typeof(Filter))!;

        var check = Assert.Single(filter.GetCheckConstraints(), c => c.Name == "CK_Filter_Subject");
        Assert.Contains("[SubjectMatchType] IS NULL AND [SubjectValue] IS NULL", check.Sql);
        Assert.Contains("[SubjectMatchType] IS NOT NULL AND [SubjectValue] IS NOT NULL", check.Sql);
    }

    [Fact]
    public void Message_YearMonth_IsStoredComputedColumn()
    {
        var model = BuildDesignTimeModel();
        var yearMonth = model.FindEntityType(typeof(Message))!.FindProperty(nameof(Message.YearMonth))!;

        Assert.Equal("YEAR([ReceivedAtUtc]) * 100 + MONTH([ReceivedAtUtc])", yearMonth.GetComputedColumnSql());
        Assert.True(yearMonth.GetIsStored());
    }

    [Fact]
    public void Message_HasCoveringStatsIndex()
    {
        var model = BuildDesignTimeModel();
        var message = model.FindEntityType(typeof(Message))!;

        Assert.Contains(message.GetIndexes(), i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["MailboxId", "SenderAddress", "YearMonth"]));
    }

    [Theory]
    [InlineData(typeof(SenderStat), MailroomViews.SenderStats)]
    [InlineData(typeof(SenderMonthlyStat), MailroomViews.SenderMonthlyStats)]
    public void StatsViews_AreKeylessViewMappings(Type entityType, string viewName)
    {
        var model = BuildDesignTimeModel();
        var entity = model.FindEntityType(entityType)!;

        Assert.Null(entity.FindPrimaryKey());
        Assert.Equal(viewName, entity.GetViewName());
    }
}
