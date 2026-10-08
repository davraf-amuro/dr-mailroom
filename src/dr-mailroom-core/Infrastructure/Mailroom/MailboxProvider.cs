using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati alle caselle: checkpoint, stato e esito dell'ultimo giro.</summary>
public class MailboxProvider(
    MailroomDbContext context,
    IDbContextFactory<MailroomDbContext> contextFactory,
    ILogger<MailboxProvider> logger)
{
    /// <summary>Carica la casella con tracking, per modificarne checkpoint e stato nello stesso SaveChanges.</summary>
    public async Task<Mailbox?> GetMailboxForUpdateAsync(int mailboxId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Carico la casella {MailboxId} per aggiornamento", mailboxId);

        // Leggo da DB la casella con tracking: il contesto è NoTracking di default
        return await context.Mailboxes.AsTracking()
            .FirstOrDefaultAsync(m => m.Id == mailboxId, cancellationToken);
    }

    /// <summary>Id delle caselle attive e collegate (con refresh token): quelle lavorate dal giro schedulato.</summary>
    public async Task<List<int>> GetActiveMailboxIdsAsync(CancellationToken cancellationToken)
    {
        // Leggo da DB le caselle attive con token
        return await context.Mailboxes
            .Where(m => m.Status == MailboxStatus.Active && m.EncryptedRefreshToken != null)
            .OrderBy(m => m.Id)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Salva le modifiche alle entità tracciate (caselle, giri).</summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Scrivo su DB le modifiche tracciate
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Scarta tutte le modifiche pendenti del contesto del giro (dopo un errore, prima di proseguire).</summary>
    public void DiscardPendingChanges() => context.ChangeTracker.Clear();

    /// <summary>Registra l'esito dell'ultimo giro sulla casella con un contesto dedicato; newStatus null = invariato.</summary>
    public async Task RecordRunOutcomeAsync(
        int mailboxId,
        string? lastError,
        MailboxStatus? newStatus,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Leggo da DB la casella con tracking, in un contesto pulito
        var mailbox = await db.Mailboxes.AsTracking().FirstAsync(m => m.Id == mailboxId, cancellationToken);
        mailbox.LastRunAtUtc = DateTime.UtcNow;
        mailbox.LastError = TextLimits.Truncate(lastError, ColumnLengths.ErrorText);
        mailbox.Status = newStatus ?? mailbox.Status;

        // Scrivo su DB l'esito sulla casella
        await db.SaveChangesAsync(cancellationToken);

        if (newStatus is not null)
        {
            logger.LogWarning("Casella {MailboxId} passata allo stato {Status}", mailboxId, newStatus);
        }
    }

    /// <summary>Caselle che rispettano il filtro, proiettate con il selector EF-traducibile.</summary>
    public async Task<List<TResult>> GetMailboxAsync<TResult>(
        MailboxFilter filter,
        Expression<Func<Mailbox, TResult>> selector,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Elenco caselle con filtro {@Filter}", filter);

        // Leggo da DB: WHERE con i soli filtri valorizzati, SELECT con le sole colonne del DTO
        return await context.Mailboxes
            .Where(filter.ToExpression())
            .OrderBy(m => m.EmailAddress)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Una casella proiettata, null se non esiste.</summary>
    public async Task<TResult?> GetMailboxByIdAsync<TResult>(
        int mailboxId,
        Expression<Func<Mailbox, TResult>> selector,
        CancellationToken cancellationToken)
    {
        // Leggo da DB la casella richiesta
        return await context.Mailboxes
            .Where(m => m.Id == mailboxId)
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Stato della casella, null se non esiste.</summary>
    public async Task<MailboxStatus?> GetMailboxStatusAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB il solo stato della casella
        return await context.Mailboxes
            .Where(m => m.Id == mailboxId)
            .Select(m => (MailboxStatus?)m.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Registra una casella collegata via OAuth: nuova → inserita da backfill; esistente → token aggiornato e stato Active.
    /// Restituisce null se l'indirizzo appartiene a una casella eliminata (D18).
    /// </summary>
    public async Task<int?> UpsertConnectedMailboxAsync(string emailAddress, string encryptedRefreshToken, CancellationToken cancellationToken)
    {
        var address = emailAddress.Trim().ToLowerInvariant();

        // Leggo da DB l'eventuale casella con lo stesso indirizzo, con tracking
        var mailbox = await context.Mailboxes.AsTracking()
            .FirstOrDefaultAsync(m => m.EmailAddress == address, cancellationToken);

        if (mailbox?.Status == MailboxStatus.Deleted)
        {
            logger.LogWarning("Collegamento rifiutato: la casella {MailboxId} è eliminata", mailbox.Id);
            return null;
        }

        if (mailbox is null)
        {
            mailbox = new Mailbox { EmailAddress = address };
            context.Mailboxes.Add(mailbox);
        }

        mailbox.EncryptedRefreshToken = encryptedRefreshToken;
        mailbox.Status = MailboxStatus.Active;
        mailbox.LastError = null;

        // Scrivo su DB la casella collegata
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Casella {MailboxId} collegata", mailbox.Id);
        return mailbox.Id;
    }

    /// <summary>Eliminazione logica (D18): stato Deleted e refresh token rimosso; i dati restano. False se non esiste.</summary>
    public async Task<bool> MarkDeletedAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB la casella con tracking
        var mailbox = await context.Mailboxes.AsTracking()
            .FirstOrDefaultAsync(m => m.Id == mailboxId, cancellationToken);
        if (mailbox is null)
        {
            return false;
        }

        mailbox.Status = MailboxStatus.Deleted;
        mailbox.EncryptedRefreshToken = null;

        // Scrivo su DB l'eliminazione logica
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Casella {MailboxId} eliminata (logicamente)", mailboxId);
        return true;
    }
}
