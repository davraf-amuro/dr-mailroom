using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura keyless della vista vw_SenderStats (creata dalla migration).</summary>
public class SenderStatConfiguration : IEntityTypeConfiguration<SenderStat>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella SenderStat.</summary>
    public void Configure(EntityTypeBuilder<SenderStat> builder)
    {
        builder.HasNoKey();
        builder.ToView(MailroomViews.SenderStats);
    }
}
