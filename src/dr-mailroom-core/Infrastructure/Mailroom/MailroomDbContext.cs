using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Contesto EF Core del database dr-mailroom: tabelle di dominio e viste statistiche.</summary>
public class MailroomDbContext(
    DbContextOptions<MailroomDbContext> options,
    ILoggerFactory loggerFactory) : DbContext(options)
{
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Filter> Filters => Set<Filter>();
    public DbSet<FilterProcess> FilterProcesses => Set<FilterProcess>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<ProcessExecution> ProcessExecutions => Set<ProcessExecution>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertFilter> AlertFilters => Set<AlertFilter>();
    public DbSet<ExtractedAttachment> ExtractedAttachments => Set<ExtractedAttachment>();
    public DbSet<ArchivedMessageContent> ArchivedMessageContents => Set<ArchivedMessageContent>();
    public DbSet<SenderStat> SenderStats => Set<SenderStat>();
    public DbSet<SenderMonthlyStat> SenderMonthlyStats => Set<SenderMonthlyStat>();

    /// <summary>Collega il logging di EF quando le opzioni non arrivano già configurate (C4).</summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && loggerFactory != null)
        {
            optionsBuilder.UseLoggerFactory(loggerFactory);
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
        }
    }

    /// <summary>Enum salvati come testo: le tabelle restano leggibili anche da SQL.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<MailboxStatus>().HaveConversion<string>().HaveMaxLength(ColumnLengths.EnumText);
        configurationBuilder.Properties<RunTrigger>().HaveConversion<string>().HaveMaxLength(ColumnLengths.EnumText);
        configurationBuilder.Properties<RunStatus>().HaveConversion<string>().HaveMaxLength(ColumnLengths.EnumText);
        configurationBuilder.Properties<SubjectMatchType>().HaveConversion<string>().HaveMaxLength(ColumnLengths.EnumText);
        configurationBuilder.Properties<ProcessType>().HaveConversion<string>().HaveMaxLength(ColumnLengths.EnumText);
        configurationBuilder.Properties<ProcessExecutionStatus>().HaveConversion<string>().HaveMaxLength(ColumnLengths.EnumText);
    }

    /// <summary>Applica le configurazioni Fluent API di questa libreria (cartella Configurations).</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MailroomDbContext).Assembly);
}
