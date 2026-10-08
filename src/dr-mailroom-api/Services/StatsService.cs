using dr_mailroom_api.Dto;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Services;

/// <summary>Servizio delle statistiche per mittente (T5, D9).</summary>
public class StatsService(StatsProvider statsProvider)
{
    /// <summary>Totale mail per mittente.</summary>
    public Task<List<SenderStatDto>> GetSenderStatsAsync(SenderStatFilter filter, CancellationToken cancellationToken) =>
        statsProvider.GetSenderStatAsync(filter, SenderStatDto.Projection, cancellationToken);

    /// <summary>Mail per mittente e mese.</summary>
    public Task<List<SenderMonthlyStatDto>> GetSenderMonthlyStatsAsync(SenderMonthlyStatFilter filter, CancellationToken cancellationToken) =>
        statsProvider.GetSenderMonthlyStatAsync(filter, SenderMonthlyStatDto.Projection, cancellationToken);
}
