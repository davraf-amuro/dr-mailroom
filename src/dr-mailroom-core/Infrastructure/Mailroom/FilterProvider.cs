using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati ai filtri con i loro processi.</summary>
public class FilterProvider(MailroomDbContext context, ILogger<FilterProvider> logger)
{
    /// <summary>Filtri attivi validi per la casella (propri o globali), con i processi.</summary>
    public async Task<List<Filter>> GetActiveFiltersForMailboxAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB i filtri attivi della casella e quelli globali (MailboxId null)
        var filters = await context.Filters
            .Include(f => f.Processes)
            .Where(f => f.IsEnabled && (f.MailboxId == null || f.MailboxId == mailboxId))
            .OrderBy(f => f.Id)
            .ToListAsync(cancellationToken);

        logger.LogDebug("Casella {MailboxId}: {FilterCount} filtri attivi", mailboxId, filters.Count);
        return filters;
    }

    /// <summary>Un filtro con i suoi processi, null se non esiste.</summary>
    public async Task<Filter?> GetFilterWithProcessesAsync(int filterId, CancellationToken cancellationToken)
    {
        // Leggo da DB il filtro con i processi associati
        return await context.Filters
            .Include(f => f.Processes)
            .FirstOrDefaultAsync(f => f.Id == filterId, cancellationToken);
    }

    /// <summary>Filtri che rispettano il filtro di ricerca, proiettati con il selector EF-traducibile.</summary>
    public async Task<List<TResult>> GetFilterAsync<TResult>(
        FilterListFilter filter,
        Expression<Func<Filter, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Elenco filtri con filtro {@Filter}", filter);

        // Leggo da DB: WHERE con i soli filtri valorizzati, SELECT con le sole colonne del DTO
        return await context.Filters
            .Where(filter.ToExpression())
            .OrderBy(f => f.Id)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Un filtro proiettato, null se non esiste.</summary>
    public async Task<TResult?> GetFilterByIdAsync<TResult>(
        int filterId,
        Expression<Func<Filter, TResult>> selector,
        CancellationToken cancellationToken)
    {
        // Leggo da DB il filtro richiesto
        return await context.Filters
            .Where(f => f.Id == filterId)
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Inserisce un filtro con i suoi processi e ne restituisce l'Id.</summary>
    public async Task<int> CreateFilterAsync(Filter filter, CancellationToken cancellationToken)
    {
        context.Filters.Add(filter);

        // Scrivo su DB filtro e processi
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Creato filtro {FilterId} sul mittente {SenderAddress}", filter.Id, filter.SenderAddress);
        return filter.Id;
    }

    /// <summary>Aggiorna campi e processi di un filtro esistente. False se non esiste.</summary>
    public async Task<bool> UpdateFilterAsync(Filter updated, CancellationToken cancellationToken)
    {
        // AsTracking: il context è NoTracking di default, senza tracking SaveChanges non rileva le modifiche
        var existing = await context.Filters.AsTracking()
            .Include(f => f.Processes)
            .FirstOrDefaultAsync(f => f.Id == updated.Id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.MailboxId = updated.MailboxId;
        existing.Name = updated.Name;
        existing.SenderAddress = updated.SenderAddress;
        existing.SubjectMatchType = updated.SubjectMatchType;
        existing.SubjectValue = updated.SubjectValue;
        existing.AttachmentSubfolder = updated.AttachmentSubfolder;
        existing.IsEnabled = updated.IsEnabled;

        var wanted = updated.Processes.Select(p => p.ProcessType).ToHashSet();
        foreach (var removed in existing.Processes.Where(p => !wanted.Contains(p.ProcessType)).ToList())
        {
            existing.Processes.Remove(removed);
        }

        foreach (var added in wanted.Where(t => existing.Processes.All(p => p.ProcessType != t)))
        {
            existing.Processes.Add(new FilterProcess { FilterId = existing.Id, ProcessType = added });
        }

        // Scrivo su DB le modifiche al filtro e ai processi
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Cancella un filtro (processi e collegamenti alle allerte a cascata). False se non esiste.</summary>
    public async Task<bool> DeleteFilterAsync(int filterId, CancellationToken cancellationToken)
    {
        // Leggo da DB il filtro con tracking
        var existing = await context.Filters.AsTracking()
            .FirstOrDefaultAsync(f => f.Id == filterId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        context.Filters.Remove(existing);

        // Scrivo su DB la cancellazione
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cancellato filtro {FilterId}", filterId);
        return true;
    }
}
