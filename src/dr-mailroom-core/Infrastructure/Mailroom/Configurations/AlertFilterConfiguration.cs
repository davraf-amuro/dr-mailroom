using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura del legame allerta-filtro: chiave composta (AlertId, FilterId).</summary>
public class AlertFilterConfiguration : IEntityTypeConfiguration<AlertFilter>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella AlertFilter.</summary>
    public void Configure(EntityTypeBuilder<AlertFilter> builder)
    {
        builder.HasKey(e => new { e.AlertId, e.FilterId });

        builder.HasOne(e => e.Alert)
            .WithMany(a => a.Filters)
            .HasForeignKey(e => e.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Filter)
            .WithMany()
            .HasForeignKey(e => e.FilterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
