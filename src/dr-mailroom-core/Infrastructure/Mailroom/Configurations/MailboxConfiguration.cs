using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura della casella: indirizzo unico (UNIQUE EmailAddress).</summary>
public class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella Mailbox.</summary>
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EmailAddress).HasMaxLength(ColumnLengths.EmailAddress);
        builder.Property(e => e.BackfillPageToken).HasMaxLength(ColumnLengths.PageToken);
        builder.Property(e => e.LastError).HasMaxLength(ColumnLengths.ErrorText);
        builder.HasIndex(e => e.EmailAddress).IsUnique();
    }
}
