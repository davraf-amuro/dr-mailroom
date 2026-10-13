using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati alle viste statistiche per mittente (T5).</summary>
public class StatsProvider(MailroomDbContext context, ILogger<StatsProvider> logger)
{
    /// <summary>Totale mail per mittente, dal mittente più attivo.</summary>
    public async Task<List<TResult>> GetSenderStatAsync<TResult>(
        SenderStatFilter filter,
        Expression<Func<SenderStat, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Statistica mittenti con filtro {@Filter}", filter);

        // Leggo da DB la vista vw_SenderStats con i soli filtri valorizzati
        return await context.SenderStats
            .Where(filter.ToExpression())
            .OrderByDescending(s => s.MailCount)
            .ThenBy(s => s.SenderAddress)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Mail per mittente e mese, in ordine di mittente e mese.</summary>
    public async Task<List<TResult>> GetSenderMonthlyStatAsync<TResult>(
        SenderMonthlyStatFilter filter,
        Expression<Func<SenderMonthlyStat, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Statistica mensile mittenti con filtro {@Filter}", filter);

        // Leggo da DB la vista vw_SenderMonthlyStats con i soli filtri valorizzati
        return await context.SenderMonthlyStats
            .Where(filter.ToExpression())
            .OrderBy(s => s.SenderAddress)
            .ThenBy(s => s.YearMonth)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Panoramica mittenti di una casella, aggregata per indirizzo: conteggio, mail più vecchia e più recente
    /// (data e oggetto; pari merito risolti sull'Id). Ordinamento e paginazione in SQL.
    /// </summary>
    public async Task<SenderOverviewPage> GetSenderOverviewAsync(
        int mailboxId,
        int? year,
        string? search,
        SenderOverviewSortBy sortBy,
        SortDirection sortDir,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Panoramica mittenti casella {MailboxId}, anno {Year}, ricerca {Search}, ordinamento {SortBy} {SortDir}, pagina {Page} da {PageSize}",
            mailboxId, year, search, sortBy, sortDir, page, pageSize);

        var scope = MessagesOf(mailboxId, year);
        var messages = search is null ? scope : scope.Where(m => m.SenderAddress.Contains(search));

        // Leggo da DB il numero di mittenti distinti che rispettano il filtro
        var totalCount = await messages.Select(m => m.SenderAddress).Distinct().CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return new SenderOverviewPage([], 0);
        }

        // Leggo da DB la pagina richiesta
        var items = await BuildSenderOverviewPageQuery(mailboxId, year, search, sortBy, sortDir, page, pageSize)
            .ToListAsync(cancellationToken);

        return new SenderOverviewPage(items, totalCount);
    }

    /// <summary>
    /// Query della pagina: aggregati per indirizzo ordinati e paginati, poi nome e oggetti con sottoquery
    /// correlate sulle sole righe della pagina. Pubblica anche per verificarne la traduzione SQL nei test.
    /// </summary>
    public IQueryable<SenderOverview> BuildSenderOverviewPageQuery(
        int mailboxId,
        int? year,
        string? search,
        SenderOverviewSortBy sortBy,
        SortDirection sortDir,
        int page,
        int pageSize)
    {
        var scope = MessagesOf(mailboxId, year);
        var messages = search is null ? scope : scope.Where(m => m.SenderAddress.Contains(search));

        var groups = messages
            .GroupBy(m => m.SenderAddress)
            .Select(g => new SenderAggregate
            {
                SenderAddress = g.Key,
                MailCount = g.Count(),
                SentCount = g.Count(m => m.IsSent),
                FirstReceivedAtUtc = g.Min(m => m.ReceivedAtUtc),
                LastReceivedAtUtc = g.Max(m => m.ReceivedAtUtc),
            });

        return Sort(groups, sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new SenderOverview(
                r.SenderAddress,
                scope.Where(m => m.SenderAddress == r.SenderAddress && m.SenderName != null)
                    .OrderByDescending(m => m.ReceivedAtUtc).ThenByDescending(m => m.Id)
                    .Select(m => m.SenderName)
                    .FirstOrDefault(),
                r.MailCount,
                r.SentCount,
                r.FirstReceivedAtUtc,
                scope.Where(m => m.SenderAddress == r.SenderAddress)
                    .OrderBy(m => m.ReceivedAtUtc).ThenBy(m => m.Id)
                    .Select(m => m.Subject)
                    .FirstOrDefault(),
                r.LastReceivedAtUtc,
                scope.Where(m => m.SenderAddress == r.SenderAddress)
                    .OrderByDescending(m => m.ReceivedAtUtc).ThenByDescending(m => m.Id)
                    .Select(m => m.Subject)
                    .FirstOrDefault()));
    }

    /// <summary>Mail per anno di ricezione, dal più recente; con senderAddress solo quel mittente (uguaglianza). Senza le cestinate.</summary>
    public async Task<List<YearStat>> GetYearStatAsync(int mailboxId, string? senderAddress, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mail per anno casella {MailboxId}, mittente {SenderAddress}", mailboxId, senderAddress);

        var messages = context.Messages.Where(m => m.MailboxId == mailboxId && m.TrashedAtUtc == null);
        if (senderAddress is not null)
        {
            messages = messages.Where(m => m.SenderAddress == senderAddress);
        }

        // Leggo da DB i conteggi raggruppati per anno
        return await messages
            .GroupBy(m => m.ReceivedAtUtc.Year)
            .Select(g => new { Year = g.Key, MailCount = g.Count() })
            .OrderByDescending(y => y.Year)
            .Select(y => new YearStat(y.Year, y.MailCount))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Mail della casella non cestinate, eventualmente limitate a un anno (intervallo su ReceivedAtUtc: usa l'indice,
    /// che include TrashedAtUtc).
    /// </summary>
    private IQueryable<Message> MessagesOf(int mailboxId, int? year)
    {
        var messages = context.Messages.Where(m => m.MailboxId == mailboxId && m.TrashedAtUtc == null);
        if (year is int y)
        {
            var from = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var to = from.AddYears(1);
            messages = messages.Where(m => m.ReceivedAtUtc >= from && m.ReceivedAtUtc < to);
        }

        return messages;
    }

    /// <summary>Ordinamento richiesto, a parità sempre per indirizzo (paginazione stabile).</summary>
    private static IQueryable<SenderAggregate> Sort(IQueryable<SenderAggregate> groups, SenderOverviewSortBy sortBy, SortDirection sortDir)
    {
        var descending = sortDir == SortDirection.Desc;
        IOrderedQueryable<SenderAggregate> ordered = sortBy switch
        {
            SenderOverviewSortBy.LastReceivedAt => descending
                ? groups.OrderByDescending(g => g.LastReceivedAtUtc) : groups.OrderBy(g => g.LastReceivedAtUtc),
            SenderOverviewSortBy.FirstReceivedAt => descending
                ? groups.OrderByDescending(g => g.FirstReceivedAtUtc) : groups.OrderBy(g => g.FirstReceivedAtUtc),
            SenderOverviewSortBy.SenderAddress => descending
                ? groups.OrderByDescending(g => g.SenderAddress) : groups.OrderBy(g => g.SenderAddress),
            _ => descending ? groups.OrderByDescending(g => g.MailCount) : groups.OrderBy(g => g.MailCount),
        };

        return sortBy == SenderOverviewSortBy.SenderAddress ? ordered : ordered.ThenBy(g => g.SenderAddress);
    }

    /// <summary>Aggregato intermedio per mittente: membri init per la traduzione EF.</summary>
    private sealed class SenderAggregate
    {
        public string SenderAddress { get; init; } = string.Empty;

        public int MailCount { get; init; }

        public int SentCount { get; init; }

        public DateTime FirstReceivedAtUtc { get; init; }

        public DateTime LastReceivedAtUtc { get; init; }
    }
}
