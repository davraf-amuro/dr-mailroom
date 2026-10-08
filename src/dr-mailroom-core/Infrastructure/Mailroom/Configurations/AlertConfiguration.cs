using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura delle allerte: una per messaggio (UNIQUE MessageId, D3) e ricerca delle non consumate.</summary>
public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella Alert.</summary>
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SenderAddress).HasMaxLength(ColumnLengths.EmailAddress);
        builder.Property(e => e.Subject).HasMaxLength(ColumnLengths.Subject);
        builder.HasIndex(e => e.MessageId).IsUnique();
        builder.HasIndex(e => e.ConsumedAtUtc);

        builder.HasOne(e => e.Message)
            .WithMany()
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
