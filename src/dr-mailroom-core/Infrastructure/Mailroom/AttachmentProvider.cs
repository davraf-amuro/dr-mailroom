using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati agli allegati salvati: base dell'idempotenza dell'estrazione (UNIQUE MessageId, PartId).</summary>
public class AttachmentProvider(MailroomDbContext context, ILogger<AttachmentProvider> logger)
{
    /// <summary>PartId degli allegati del messaggio già salvati su disco.</summary>
    public async Task<HashSet<string>> GetExtractedPartIdsAsync(long messageId, CancellationToken cancellationToken)
    {
        // Leggo da DB le parti già estratte del messaggio
        var partIds = await context.ExtractedAttachments
            .Where(a => a.MessageId == messageId)
            .Select(a => a.PartId)
            .ToListAsync(cancellationToken);

        return partIds.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Registra un allegato appena scritto su disco.</summary>
    public async Task AddAsync(ExtractedAttachment attachment, CancellationToken cancellationToken)
    {
        context.ExtractedAttachments.Add(attachment);

        try
        {
            // Scrivo su DB l'allegato salvato
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Stacco l'entità: un SaveChanges successivo sullo stesso contesto non deve riprovare l'insert
            context.Entry(attachment).State = EntityState.Detached;
            throw;
        }

        logger.LogInformation(
            "Allegato {PartId} del messaggio {MessageId} salvato in {RelativePath} ({SizeBytes} byte)",
            attachment.PartId, attachment.MessageId, attachment.RelativePath, attachment.SizeBytes);
    }

    /// <summary>Allegati salvati per un messaggio, per l'endpoint di consultazione.</summary>
    public async Task<List<TResult>> GetAttachmentsAsync<TResult>(
        long messageId,
        System.Linq.Expressions.Expression<Func<ExtractedAttachment, TResult>> selector,
        CancellationToken cancellationToken)
    {
        // Leggo da DB gli allegati del messaggio, proiettati sul DTO richiesto
        return await context.ExtractedAttachments
            .Where(a => a.MessageId == messageId)
            .OrderBy(a => a.PartId)
            .Select(selector)
            .ToListAsync(cancellationToken);
    }
}
