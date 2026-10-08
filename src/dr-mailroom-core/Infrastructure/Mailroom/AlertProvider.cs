using System.Linq.Expressions;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati alle allerte: una per messaggio (D3), collegata a ogni filtro che l'ha generata.</summary>
public class AlertProvider(MailroomDbContext context, ILogger<AlertProvider> logger)
{
    /// <summary>
    /// Crea l'allerta del messaggio se manca e collega i filtri indicati non ancora collegati.
    /// Restituisce true se l'allerta è stata creata ora.
    /// </summary>
    public async Task<bool> EnsureAlertAsync(Message message, IReadOnlyCollection<int> filterIds, CancellationToken cancellationToken)
    {
        // Leggo da DB l'eventuale allerta del messaggio con i filtri collegati, con tracking
        var alert = await context.Alerts.AsTracking()
            .Include(a => a.Filters)
            .FirstOrDefaultAsync(a => a.MessageId == message.Id, cancellationToken);

        var created = alert is null;
        if (alert is null)
        {
            alert = new Alert
            {
                MessageId = message.Id,
                SenderAddress = message.SenderAddress,
                Subject = TextLimits.Truncate(message.Subject, ColumnLengths.Subject),
                ReceivedAtUtc = message.ReceivedAtUtc,
                CreatedAtUtc = DateTime.UtcNow,
            };
            context.Alerts.Add(alert);
        }

        var linked = alert.Filters.Select(f => f.FilterId).ToHashSet();
        foreach (var filterId in filterIds.Where(id => !linked.Contains(id)))
        {
            alert.Filters.Add(new AlertFilter { Alert = alert, FilterId = filterId });
        }

        // Scrivo su DB allerta e collegamenti ai filtri in un unico SaveChanges
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Allerta per il messaggio {MessageId}: {Outcome}, filtri collegati {FilterCount}",
            message.Id, created ? "creata" : "già presente", alert.Filters.Count);
        return created;
    }

    /// <summary>Allerte che rispettano il filtro, dalla più vecchia, proiettate con il selector EF-traducibile.</summary>
    public async Task<List<TResult>> GetAlertAsync<TResult>(
        AlertListFilter filter,
        Expression<Func<Alert, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Elenco allerte con filtro {@Filter}", filter);

        // Leggo da DB: WHERE con i soli filtri valorizzati, SELECT con le sole colonne del DTO
        return await context.Alerts
            .Where(filter.ToExpression())
            .OrderBy(a => a.ReceivedAtUtc)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Marca l'allerta come consumata (idempotente: la prima data resta). False se non esiste.</summary>
    public async Task<bool> ConsumeAlertAsync(long alertId, CancellationToken cancellationToken)
    {
        // Leggo da DB l'allerta con tracking
        var alert = await context.Alerts.AsTracking()
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);
        if (alert is null)
        {
            return false;
        }

        if (alert.ConsumedAtUtc is null)
        {
            alert.ConsumedAtUtc = DateTime.UtcNow;

            // Scrivo su DB la data di consumo
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Allerta {AlertId} consumata", alertId);
        }

        return true;
    }
}
