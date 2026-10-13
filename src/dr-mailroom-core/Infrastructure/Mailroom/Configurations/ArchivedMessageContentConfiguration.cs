using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura del contenuto archiviato: al più uno per messaggio, eliminato in cascata con la mail.</summary>
public class ArchivedMessageContentConfiguration : IEntityTypeConfiguration<ArchivedMessageContent>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella ArchivedMessageContent.</summary>
    public void Configure(EntityTypeBuilder<ArchivedMessageContent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RelativePath).HasMaxLength(ColumnLengths.RelativePath);
        builder.Property(e => e.Sha256).HasMaxLength(ColumnLengths.Sha256Hex).IsFixedLength();
        builder.HasIndex(e => e.MessageId).IsUnique();

        builder.HasOne(e => e.Message)
            .WithMany()
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
