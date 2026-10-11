using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati ai messaggi indicizzati: insert idempotente per (casella, id Gmail) (T4).</summary>
public class MessageProvider(MailroomDbContext context, ILogger<MessageProvider> logger)
{
    /// <summary>Codici SQL Server di violazione di indice UNIQUE / chiave: il messaggio è già presente.</summary>
    private static readonly int[] _uniqueViolationNumbers = [2601, 2627];

    /// <summary>
    /// Inserisce i messaggi non ancora presenti e salva, nello stesso SaveChanges, le altre modifiche tracciate
    /// (checkpoint della casella, contatori del giro). Restituisce i soli messaggi inseriti, con Id valorizzato.
    /// </summary>
    public async Task<IReadOnlyList<Message>> InsertNewMessagesAsync(
        int mailboxId,
        IReadOnlyList<Message> candidates,
        CancellationToken cancellationToken)
    {
        var gmailIds = candidates.Select(c => c.GmailMessageId).Distinct().ToList();

        // Leggo da DB quali id Gmail della casella esistono già
        var existing = await context.Messages
            .Where(m => m.MailboxId == mailboxId && gmailIds.Contains(m.GmailMessageId))
            .Select(m => m.GmailMessageId)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var toInsert = candidates
            .Where(c => !existingSet.Contains(c.GmailMessageId))
            .DistinctBy(c => c.GmailMessageId)
            .ToList();

        context.Messages.AddRange(toInsert);

        try
        {
            // Scrivo su DB messaggi nuovi + checkpoint + contatori in un'unica transazione
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Inserimento concorrente (non previsto sotto lock): ripiego riga per riga, i duplicati si saltano
            logger.LogWarning(ex, "Violazione UNIQUE inserendo {Count} messaggi della casella {MailboxId}: ripiego riga per riga", toInsert.Count, mailboxId);
            return await InsertOneByOneAsync(toInsert, cancellationToken);
        }

        logger.LogInformation(
            "Casella {MailboxId}: {Inserted} messaggi nuovi su {Candidates} letti",
            mailboxId, toInsert.Count, candidates.Count);
        return toInsert;
    }

    /// <summary>Messaggi con gli Id indicati, ordinati per data di ricezione.</summary>
    public async Task<List<Message>> GetMessagesByIdsAsync(IReadOnlyCollection<long> messageIds, CancellationToken cancellationToken)
    {
        // Leggo da DB i messaggi appena inseriti, da passare ai filtri
        return await context.Messages
            .Where(m => messageIds.Contains(m.Id))
            .OrderBy(m => m.ReceivedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Messaggi della casella salvati con data 1970-01-01 (internalDate assente, ripiego su 0 delle versioni precedenti),
    /// tracciati per l'aggiornamento della data.
    /// </summary>
    public async Task<List<Message>> GetUndatedMessagesAsync(int mailboxId, int max, CancellationToken cancellationToken)
    {
        // Leggo da DB i messaggi con data 1970-01-01 con tracking: il contesto è NoTracking di default e li aggiorno
        return await context.Messages.AsTracking()
            .Where(m => m.MailboxId == mailboxId && m.ReceivedAtUtc <= DateTime.UnixEpoch)
            .OrderBy(m => m.Id)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Salva le modifiche tracciate (date riparate).</summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Scrivo su DB le date aggiornate
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Messaggi già indicizzati di un mittente nella casella: candidati della riapplica (D13).</summary>
    public async Task<List<Message>> GetMessagesBySenderAsync(int mailboxId, string senderAddress, CancellationToken cancellationToken)
    {
        var sender = senderAddress.Trim().ToLowerInvariant();

        // Leggo da DB i messaggi del mittente (salvato sempre minuscolo, D7)
        return await context.Messages
            .Where(m => m.MailboxId == mailboxId && m.SenderAddress == sender)
            .OrderBy(m => m.ReceivedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Messaggi già indicizzati di un mittente in tutte le caselle: candidati dell'anteprima di un filtro globale.</summary>
    public async Task<List<Message>> GetMessagesBySenderInAllMailboxesAsync(string senderAddress, CancellationToken cancellationToken)
    {
        var sender = senderAddress.Trim().ToLowerInvariant();

        // Leggo da DB i messaggi del mittente in ogni casella
        return await context.Messages
            .Where(m => m.SenderAddress == sender)
            .OrderByDescending(m => m.ReceivedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Pagina delle mail che rispettano il filtro (già normalizzato dal service), ordinate per data di ricezione
    /// con Id come spareggio, proiettate con il selector EF-traducibile, più il totale per la paginazione.
    /// </summary>
    public async Task<(List<TResult> Items, int TotalCount)> GetSenderMessagesPageAsync<TResult>(
        SenderMessageFilter filter,
        SortDirection sortDir,
        int page,
        int pageSize,
        Expression<Func<Message, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Mail del mittente {SenderAddress} nella casella {MailboxId}, ricerca {Search}, verso {SortDir}, pagina {Page} da {PageSize}",
            filter.SenderAddress, filter.MailboxId, filter.Search, sortDir, page, pageSize);

        var messages = context.Messages.Where(filter.ToExpression());

        // Leggo da DB il numero di mail che rispettano il filtro
        var totalCount = await messages.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return ([], 0);
        }

        var ordered = sortDir == SortDirection.Asc
            ? messages.OrderBy(m => m.ReceivedAtUtc).ThenBy(m => m.Id)
            : messages.OrderByDescending(m => m.ReceivedAtUtc).ThenByDescending(m => m.Id);

        // Leggo da DB la pagina richiesta, con le sole colonne del DTO
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>True se il messaggio esiste.</summary>
    public async Task<bool> MessageExistsAsync(long messageId, CancellationToken cancellationToken)
    {
        // Leggo da DB l'esistenza del messaggio
        return await context.Messages.AnyAsync(m => m.Id == messageId, cancellationToken);
    }

    /// <summary>Ripiego idempotente: salva un messaggio alla volta e scarta quelli già presenti.</summary>
    private async Task<IReadOnlyList<Message>> InsertOneByOneAsync(List<Message> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            context.Entry(message).State = EntityState.Detached;
        }

        // Le altre modifiche tracciate (checkpoint, contatori) vanno salvate anche se nessun messaggio entra
        await context.SaveChangesAsync(cancellationToken);

        var inserted = new List<Message>();
        foreach (var message in messages)
        {
            context.Messages.Add(message);
            try
            {
                // Scrivo su DB il singolo messaggio
                await context.SaveChangesAsync(cancellationToken);
                inserted.Add(message);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                context.Entry(message).State = EntityState.Detached;
            }
        }

        return inserted;
    }

    /// <summary>True se l'eccezione nasce da una violazione di indice UNIQUE o di chiave primaria.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql && _uniqueViolationNumbers.Contains(sql.Number);
}
