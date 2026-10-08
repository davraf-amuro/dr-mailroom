using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dr_mailroom_core.Infrastructure.Mailroom.Configurations;

/// <summary>Mappatura keyless della vista vw_SenderMonthlyStats (creata dalla migration).</summary>
public class SenderMonthlyStatConfiguration : IEntityTypeConfiguration<SenderMonthlyStat>
{
    /// <summary>Chiavi, lunghezze, vincoli e indici della tabella SenderMonthlyStat.</summary>
    public void Configure(EntityTypeBuilder<SenderMonthlyStat> builder)
    {
        builder.HasNoKey();
        builder.ToView(MailroomViews.SenderMonthlyStats);
    }
}
