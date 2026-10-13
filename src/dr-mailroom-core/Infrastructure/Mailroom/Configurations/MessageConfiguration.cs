using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura dei messaggi: idempotenza per casella (T4) e colonna YearMonth calcolata (T5).</summary>
public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella Message.</summary>
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.GmailMessageId).HasMaxLength(ColumnLengths.GmailId);
        builder.Property(e => e.ThreadId).HasMaxLength(ColumnLengths.GmailId);
        builder.Property(e => e.SenderAddress).HasMaxLength(ColumnLengths.EmailAddress);
        builder.Property(e => e.SenderName).HasMaxLength(ColumnLengths.DisplayName);
        builder.Property(e => e.Subject).HasMaxLength(ColumnLengths.Subject);
        builder.Property(e => e.Labels).HasMaxLength(ColumnLengths.Labels);
        builder.Property(e => e.IsSent).HasDefaultValue(false);

        // YEAR e MONTH sono deterministici: la colonna può essere persistita e indicizzata
        builder.Property(e => e.YearMonth)
            .HasComputedColumnSql("YEAR([ReceivedAtUtc]) * 100 + MONTH([ReceivedAtUtc])", stored: true);

        builder.HasIndex(e => new { e.MailboxId, e.GmailMessageId }).IsUnique();
        builder.HasIndex(e => new { e.MailboxId, e.SenderAddress, e.YearMonth });

        // Panoramica mittenti: raggruppamento per indirizzo, prima/ultima mail, il loro oggetto, le inviate e le cestinate senza accedere alla tabella
        builder.HasIndex(e => new { e.MailboxId, e.SenderAddress, e.ReceivedAtUtc })
            .IncludeProperties(e => new { e.Subject, e.IsSent, e.TrashedAtUtc });

        // Purga delle cestinate oltre la ritenzione
        builder.HasIndex(e => e.TrashedAtUtc).HasFilter("[TrashedAtUtc] IS NOT NULL");

        builder.HasOne(e => e.Mailbox)
            .WithMany(m => m.Messages)
            .HasForeignKey(e => e.MailboxId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
