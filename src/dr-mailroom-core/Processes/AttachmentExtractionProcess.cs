using System.Security.Cryptography;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Processes;

/// <summary>
/// Estrae gli allegati di un messaggio nella cartella del filtro (T10). Idempotente: le parti già registrate si saltano,
/// quindi una ripresa dopo un crash scrive solo quelle mancanti.
/// </summary>
public class AttachmentExtractionProcess(
    IOptions<AttachmentsOptions> options,
    AttachmentProvider attachmentProvider,
    ProcessExecutionProvider executionProvider,
    ILogger<AttachmentExtractionProcess> logger) : IMessageProcess
{
    /// <summary>Prefisso dei file temporanei, rinominati nel nome finale solo a scrittura completa.</summary>
    public const string TempFilePrefix = ".tmp-";

    /// <summary>Tentativi di rinomina quando consumatori paralleli si contendono lo stesso nome file.</summary>
    public const int MaxRenameAttempts = 10;

    public ProcessType Type => ProcessType.AttachmentExtraction;

    /// <summary>Scarica e salva le parti allegato mancanti; registra l'esito in ProcessExecution.</summary>
    public async Task<ProcessExecutionStatus> ExecuteAsync(ProcessContext context, CancellationToken cancellationToken)
    {
        var message = context.Message;
        try
        {
            var directory = SafeFileName.ResolveDirectory(options.Value.RootPath, context.Match.AttachmentSubfolder);

            // Chiamo Gmail: elenco delle parti allegato
            var parts = await context.Session.GetAttachmentPartsAsync(message.GmailMessageId, cancellationToken);

            // Leggo da DB le parti già salvate in esecuzioni precedenti
            var alreadySaved = await attachmentProvider.GetExtractedPartIdsAsync(message.Id, cancellationToken);

            var skipped = new List<string>();
            var saved = 0;
            foreach (var part in parts.Where(p => !alreadySaved.Contains(p.PartId)))
            {
                if (part.SizeBytes > options.Value.MaxSizeBytes)
                {
                    skipped.Add($"{part.FileName}: {part.SizeBytes} byte oltre il limite di {options.Value.MaxSizeBytes}");
                    continue;
                }

                await SavePartAsync(context.Session, message, part, directory, cancellationToken);
                saved++;
            }

            var status = saved == 0 && skipped.Count > 0 ? ProcessExecutionStatus.Skipped : ProcessExecutionStatus.Succeeded;
            var note = skipped.Count > 0 ? "Allegati saltati: " + string.Join("; ", skipped) : null;

            // Scrivo su DB l'esito del processo
            await executionProvider.RecordAsync(message.Id, Type, status, note, cancellationToken);
            return status;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TokenRevokedException)
        {
            logger.LogError(ex, "Estrazione allegati fallita per il messaggio {MessageId}", message.Id);

            // Scrivo su DB l'esito di errore: il giro prosegue con gli altri messaggi
            await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Failed, ex.Message, cancellationToken);
            return ProcessExecutionStatus.Failed;
        }
    }

    /// <summary>Scarica una parte, la scrive su file temporaneo, la rinomina nel nome finale e la registra a DB.</summary>
    private async Task SavePartAsync(
        IGmailSession session,
        Message message,
        GmailAttachmentPart part,
        string directory,
        CancellationToken cancellationToken)
    {
        // Chiamo Gmail: contenuto dell'allegato
        var content = await session.DownloadAttachmentAsync(message.GmailMessageId, part, cancellationToken);
        if (content.LongLength > options.Value.MaxSizeBytes)
        {
            throw new InvalidOperationException($"Allegato {part.FileName} oltre il limite di dimensione dopo il download");
        }

        Directory.CreateDirectory(directory);
        var fileName = SafeFileName.Build(message.ReceivedAtUtc, message.SenderAddress, part.FileName);
        var tempPath = Path.Combine(directory, TempFilePrefix + Guid.NewGuid().ToString("N"));
        string? finalPath = null;

        try
        {
            // Scrivo su disco il file temporaneo, poi lo rinomino: niente file finali a metà
            await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
            finalPath = MoveToAvailablePath(tempPath, directory, fileName);

            var root = Path.GetFullPath(options.Value.RootPath);
            await attachmentProvider.AddAsync(new ExtractedAttachment
            {
                MessageId = message.Id,
                PartId = part.PartId,
                OriginalFileName = TextLimits.Truncate(part.FileName, ColumnLengths.FileName)!,
                RelativePath = Path.GetRelativePath(root, finalPath).Replace('\\', '/'),
                SizeBytes = content.LongLength,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
                SavedAtUtc = DateTime.UtcNow,
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

    /// <summary>
    /// Rinomina il temporaneo nel primo nome libero. Se un altro consumatore della coda occupa lo stesso nome
    /// tra la scelta e la rinomina, ricalcola il nome e riprova.
    /// </summary>
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
            catch (IOException) when (attempt < MaxRenameAttempts && File.Exists(candidate) && File.Exists(tempPath))
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
