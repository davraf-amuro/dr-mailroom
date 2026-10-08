using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura degli allegati salvati: un allegato per (messaggio, parte) (T10).</summary>
public class ExtractedAttachmentConfiguration : IEntityTypeConfiguration<ExtractedAttachment>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella ExtractedAttachment.</summary>
    public void Configure(EntityTypeBuilder<ExtractedAttachment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PartId).HasMaxLength(ColumnLengths.PartId);
        builder.Property(e => e.OriginalFileName).HasMaxLength(ColumnLengths.FileName);
        builder.Property(e => e.RelativePath).HasMaxLength(ColumnLengths.RelativePath);
        builder.Property(e => e.Sha256).HasMaxLength(ColumnLengths.Sha256Hex).IsFixedLength();
        builder.HasIndex(e => new { e.MessageId, e.PartId }).IsUnique();

        builder.HasOne(e => e.Message)
            .WithMany()
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
