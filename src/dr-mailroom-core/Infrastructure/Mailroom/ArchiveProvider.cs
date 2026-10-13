using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati al contenuto archiviato delle mail (.eml): base dell'idempotenza di ContentArchive (UNIQUE MessageId).</summary>
public class ArchiveProvider(MailroomDbContext context, ILogger<ArchiveProvider> logger)
{
    /// <summary>Vero se il contenuto del messaggio è già salvato su disco.</summary>
    public async Task<bool> ExistsAsync(long messageId, CancellationToken cancellationToken)
    {
        // Leggo da DB se il messaggio ha già un contenuto archiviato
        return await context.ArchivedMessageContents.AnyAsync(a => a.MessageId == messageId, cancellationToken);
    }

    /// <summary>Registra un contenuto appena scritto su disco.</summary>
    public async Task AddAsync(ArchivedMessageContent content, CancellationToken cancellationToken)
    {
        context.ArchivedMessageContents.Add(content);

        try
        {
            // Scrivo su DB il contenuto archiviato
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Stacco l'entità: un SaveChanges successivo sullo stesso contesto non deve riprovare l'insert
            context.Entry(content).State = EntityState.Detached;
            throw;
        }

        logger.LogInformation(
            "Contenuto del messaggio {MessageId} archiviato in {RelativePath} ({SizeBytes} byte)",
            content.MessageId, content.RelativePath, content.SizeBytes);
    }

    /// <summary>Contenuto archiviato di un messaggio non cestinato, o null.</summary>
    public async Task<ArchivedMessageContent?> GetAsync(long messageId, CancellationToken cancellationToken)
    {
        // Leggo da DB il contenuto archiviato con i dati della mail per il nome del file
        return await context.ArchivedMessageContents
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.MessageId == messageId && a.Message!.TrashedAtUtc == null, cancellationToken);
    }

    /// <summary>Id dei messaggi non cestinati di un mittente (in una casella o in tutte) che hanno il contenuto archiviato.</summary>
    public async Task<HashSet<long>> GetArchivedMessageIdsBySenderAsync(int? mailboxId, string senderAddress, CancellationToken cancellationToken)
    {
        var sender = senderAddress.Trim().ToLowerInvariant();

        // Leggo da DB quali mail del mittente hanno il contenuto archiviato, senza passare liste di id
        var query = context.ArchivedMessageContents
            .Where(a => a.Message!.SenderAddress == sender && a.Message.TrashedAtUtc == null);
        if (mailboxId is int id)
        {
            query = query.Where(a => a.Message!.MailboxId == id);
        }

        var ids = await query.Select(a => a.MessageId).ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <summary>Percorsi relativi dei contenuti archiviati delle mail cestinate prima della soglia: da leggere prima della purga.</summary>
    public async Task<List<string>> GetPathsOfTrashedBeforeAsync(DateTime thresholdUtc, CancellationToken cancellationToken)
    {
        // Leggo da DB i file da cancellare insieme alle mail che la purga elimina
        return await context.ArchivedMessageContents
            .Where(a => a.Message!.TrashedAtUtc != null && a.Message.TrashedAtUtc < thresholdUtc)
            .Select(a => a.RelativePath)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Cancella dal disco i file archiviati indicati, solo se restano sotto la root. Un file mancante o non
    /// cancellabile si logga e non ferma gli altri. Restituisce quanti file sono stati cancellati.
    /// </summary>
    public int DeleteFiles(string rootPath, IEnumerable<string> relativePaths)
    {
        var deleted = 0;
        foreach (var relativePath in relativePaths)
        {
            var fullPath = ResolveUnderRoot(rootPath, relativePath);
            if (fullPath is null)
            {
                logger.LogWarning("Contenuto archiviato {RelativePath} fuori dalla root: file non cancellato", relativePath);
                continue;
            }

            try
            {
                if (File.Exists(fullPath))
                {
                    // Cancello dal disco il file archiviato
                    File.Delete(fullPath);
                    deleted++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Contenuto archiviato {RelativePath} non cancellato dal disco", relativePath);
            }
        }

        return deleted;
    }

    /// <summary>
    /// Percorso assoluto di un file archiviato, solo se resta sotto la root: un percorso relativo alterato nel DB
    /// non deve mai far leggere o cancellare file fuori dalla cartella configurata.
    /// </summary>
    public static string? ResolveUnderRoot(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var root = Path.GetFullPath(rootPath);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', '/')));
        return fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal) ? fullPath : null;
    }
}
