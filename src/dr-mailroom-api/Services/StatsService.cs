using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Services;

/// <summary>Servizio delle statistiche per mittente (T5, D9).</summary>
public class StatsService(StatsProvider statsProvider, MailboxProvider mailboxProvider)
{
    /// <summary>Totale mail per mittente.</summary>
    public Task<List<SenderStatDto>> GetSenderStatsAsync(SenderStatFilter filter, CancellationToken cancellationToken) =>
        statsProvider.GetSenderStatAsync(filter, SenderStatDto.Projection, cancellationToken);

    /// <summary>Mail per mittente e mese.</summary>
    public Task<List<SenderMonthlyStatDto>> GetSenderMonthlyStatsAsync(SenderMonthlyStatFilter filter, CancellationToken cancellationToken) =>
        statsProvider.GetSenderMonthlyStatAsync(filter, SenderMonthlyStatDto.Projection, cancellationToken);

    /// <summary>
    /// Panoramica paginata dei mittenti di una casella (filtro già validato: MailboxId presente).
    /// Default: dal mittente con più mail, pagina 1 da 25. NotFound se la casella non esiste.
    /// </summary>
    public async Task<ServiceResult<SenderOverviewPageDto>> GetSenderOverviewAsync(SenderOverviewFilter filter, CancellationToken cancellationToken)
    {
        var mailboxId = filter.MailboxId!.Value;
        if (await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken) is null)
        {
            return ServiceResult<SenderOverviewPageDto>.NotFound(ServiceMessages.MailboxNotFound);
        }

        var page = filter.Page ?? SenderOverviewFilter.DefaultPage;
        var pageSize = filter.PageSize ?? SenderOverviewFilter.DefaultPageSize;
        var sortBy = filter.SortBy ?? SenderOverviewSortBy.MailCount;
        var sortDir = filter.SortDir ?? (sortBy == SenderOverviewSortBy.SenderAddress ? SortDirection.Asc : SortDirection.Desc);

        var result = await statsProvider.GetSenderOverviewAsync(
            mailboxId, filter.Year, NormalizeAddress(filter.Search), sortBy, sortDir, page, pageSize, cancellationToken);

        return ServiceResult<SenderOverviewPageDto>.Ok(new SenderOverviewPageDto(
            [.. result.Items.Select(SenderOverviewDto.From)], page, pageSize, result.TotalCount));
    }

    /// <summary>Mail per anno della casella o di un suo mittente (indirizzo esatto). NotFound se la casella non esiste.</summary>
    public async Task<ServiceResult<List<YearStatDto>>> GetYearStatsAsync(YearStatFilter filter, CancellationToken cancellationToken)
    {
        var mailboxId = filter.MailboxId!.Value;
        if (await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken) is null)
        {
            return ServiceResult<List<YearStatDto>>.NotFound(ServiceMessages.MailboxNotFound);
        }

        var years = await statsProvider.GetYearStatAsync(mailboxId, NormalizeAddress(filter.SenderAddress), cancellationToken);
        return ServiceResult<List<YearStatDto>>.Ok([.. years.Select(y => new YearStatDto(y.Year, y.MailCount))]);
    }

    /// <summary>Gli indirizzi sono salvati in minuscolo (D7): testo vuoto = nessun filtro.</summary>
    private static string? NormalizeAddress(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
