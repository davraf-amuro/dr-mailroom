using System.Security.Cryptography;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Processes;

/// <summary>
/// Salva il messaggio originale (.eml, Gmail format=raw) nella cartella del filtro, come gli allegati (T10).
/// Idempotente: un messaggio già archiviato si salta, quindi riapplicare il filtro scrive solo le mail mancanti.
/// </summary>
public class ContentArchiveProcess(
    IOptions<AttachmentsOptions> options,
    ArchiveProvider archiveProvider,
    ProcessExecutionProvider executionProvider,
    ILogger<ContentArchiveProcess> logger) : IMessageProcess
{
    /// <summary>Estensione del file archiviato.</summary>
    public const string FileExtension = ".eml";

    /// <summary>Nome usato al posto dell'oggetto quando la mail non ne ha.</summary>
    public const string FallbackSubject = "mail";

    public ProcessType Type => ProcessType.ContentArchive;

    /// <summary>Scarica e salva il .eml se manca; registra l'esito in ProcessExecution.</summary>
    public async Task<ProcessExecutionStatus> ExecuteAsync(ProcessContext context, CancellationToken cancellationToken)
    {
        var message = context.Message;
        try
        {
            // Leggo da DB se il contenuto è già archiviato
            if (await archiveProvider.ExistsAsync(message.Id, cancellationToken))
            {
                await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Skipped, "Contenuto già archiviato", cancellationToken);
                return ProcessExecutionStatus.Skipped;
            }

            var directory = SafeFileName.ResolveDirectory(options.Value.RootPath, Subfolder(context));

            // Chiamo Gmail: messaggio originale
            var content = await context.Session.GetRawAsync(message.GmailMessageId, cancellationToken);
            if (content.LongLength > options.Value.MaxSizeBytes)
            {
                var note = $"Mail di {content.LongLength} byte oltre il limite di {options.Value.MaxSizeBytes}";
                await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Skipped, note, cancellationToken);
                return ProcessExecutionStatus.Skipped;
            }

            await SaveAsync(message, content, directory, cancellationToken);

            // Scrivo su DB l'esito del processo
            await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Succeeded, null, cancellationToken);
            return ProcessExecutionStatus.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TokenRevokedException)
        {
            logger.LogError(ex, "Archiviazione del contenuto fallita per il messaggio {MessageId}", message.Id);

            // Scrivo su DB l'esito di errore: il giro prosegue con gli altri messaggi
            await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Failed, ex.Message, cancellationToken);
            return ProcessExecutionStatus.Failed;
        }
    }

    /// <summary>Sottocartella del primo filtro (Id più basso) che prevede l'archiviazione; null = radice.</summary>
    private static string? Subfolder(ProcessContext context) => context.Match.MatchedFilters
        .FirstOrDefault(f => f.Processes.Any(p => p.ProcessType == ProcessType.ContentArchive))?
        .AttachmentSubfolder;

    /// <summary>Scrive il .eml su file temporaneo, lo rinomina nel primo nome libero e lo registra a DB.</summary>
    private async Task SaveAsync(Message message, byte[] content, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var subject = string.IsNullOrWhiteSpace(message.Subject) ? FallbackSubject : message.Subject;
        var fileName = SafeFileName.Build(message.ReceivedAtUtc, message.SenderAddress, subject + FileExtension);
        var tempPath = Path.Combine(directory, AttachmentExtractionProcess.TempFilePrefix + Guid.NewGuid().ToString("N"));
        string? finalPath = null;

        try
        {
            // Scrivo su disco il file temporaneo, poi lo rinomino: niente file finali a metà
            await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
            finalPath = MoveToAvailablePath(tempPath, directory, fileName);

            var root = Path.GetFullPath(options.Value.RootPath);
            await archiveProvider.AddAsync(new ArchivedMessageContent
            {
                MessageId = message.Id,
                RelativePath = Path.GetRelativePath(root, finalPath).Replace('\\', '/'),
                SizeBytes = content.LongLength,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
                ArchivedAtUtc = DateTime.UtcNow,
            }, cancellationToken);
        }
        catch
        {
            // Registrazione fallita: rimuovo il file per non lasciare copie orfane alla ripresa
            DeleteQuietly(tempPath);
            if (finalPath is not null)
            {
                DeleteQuietly(finalPath);
            }

            throw;
        }
    }

    /// <summary>Rinomina il temporaneo nel primo nome libero, riprovando se un consumatore parallelo occupa lo stesso nome.</summary>
    private static string MoveToAvailablePath(string tempPath, string directory, string fileName)
    {
        for (var attempt = 1; ; attempt++)
        {
            var candidate = SafeFileName.GetAvailablePath(directory, fileName);
            try
            {
                File.Move(tempPath, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (attempt < AttachmentExtractionProcess.MaxRenameAttempts && File.Exists(candidate) && File.Exists(tempPath))
            {
                // Nome preso nel frattempo da un altro consumatore: ne cerco un altro
            }
        }
    }

    /// <summary>Cancella un file ignorando l'assenza.</summary>
    private static void DeleteQuietly(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
