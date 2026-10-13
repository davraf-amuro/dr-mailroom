using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura dei giri: cronologia per casella ordinata per inizio.</summary>
public class RunConfiguration : IEntityTypeConfiguration<Run>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella Run.</summary>
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RequestedProcesses).HasMaxLength(ColumnLengths.RequestedProcesses);
        builder.Property(e => e.ErrorSummary).HasMaxLength(ColumnLengths.ErrorText);
        builder.Property(e => e.SenderAddress).HasMaxLength(ColumnLengths.EmailAddress);
        builder.HasIndex(e => new { e.MailboxId, e.StartedAtUtc });

        builder.HasOne(e => e.Mailbox)
            .WithMany(m => m.Runs)
            .HasForeignKey(e => e.MailboxId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cancellare un filtro conserva la cronologia dei giri di riapplica
        builder.HasOne(e => e.Filter)
            .WithMany()
            .HasForeignKey(e => e.FilterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
