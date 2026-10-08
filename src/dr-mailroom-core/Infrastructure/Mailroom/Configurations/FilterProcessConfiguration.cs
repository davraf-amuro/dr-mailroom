using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura dei processi di un filtro: chiave composta (FilterId, ProcessType).</summary>
public class FilterProcessConfiguration : IEntityTypeConfiguration<FilterProcess>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella FilterProcess.</summary>
    public void Configure(EntityTypeBuilder<FilterProcess> builder)
    {
        builder.HasKey(e => new { e.FilterId, e.ProcessType });

        builder.HasOne(e => e.Filter)
            .WithMany(f => f.Processes)
            .HasForeignKey(e => e.FilterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
