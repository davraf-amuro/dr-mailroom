using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura dei filtri: condizione sull'oggetto tutta valorizzata o tutta assente (CHECK, D4).</summary>
public class FilterConfiguration : IEntityTypeConfiguration<Filter>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella Filter.</summary>
    public void Configure(EntityTypeBuilder<Filter> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(ColumnLengths.FilterName);
        builder.Property(e => e.SenderAddress).HasMaxLength(ColumnLengths.EmailAddress);
        builder.Property(e => e.SubjectValue).HasMaxLength(ColumnLengths.SubjectValue);
        builder.Property(e => e.AttachmentSubfolder).HasMaxLength(ColumnLengths.Subfolder);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Filter_Subject",
            "([SubjectMatchType] IS NULL AND [SubjectValue] IS NULL) OR ([SubjectMatchType] IS NOT NULL AND [SubjectValue] IS NOT NULL)"));

        builder.HasIndex(e => e.SenderAddress);

        // NoAction: evita percorsi di cascata multipli verso AlertFilter e Run (vincolo SQL Server)
        builder.HasOne(e => e.Mailbox)
            .WithMany(m => m.Filters)
            .HasForeignKey(e => e.MailboxId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
