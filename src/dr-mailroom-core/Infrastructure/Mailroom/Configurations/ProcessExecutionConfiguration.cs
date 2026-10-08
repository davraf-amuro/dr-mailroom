using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura degli esiti dei processi: un solo esito per (messaggio, processo).</summary>
public class ProcessExecutionConfiguration : IEntityTypeConfiguration<ProcessExecution>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella ProcessExecution.</summary>
    public void Configure(EntityTypeBuilder<ProcessExecution> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Error).HasMaxLength(ColumnLengths.ErrorText);
        builder.HasIndex(e => new { e.MessageId, e.ProcessType }).IsUnique();

        builder.HasOne(e => e.Message)
            .WithMany()
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
