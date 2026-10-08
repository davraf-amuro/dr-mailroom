using System.Linq.Expressions;
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
}
