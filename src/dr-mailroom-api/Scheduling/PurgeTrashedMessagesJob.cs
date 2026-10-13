using dr_mailroom_core.Configuration;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.Extensions.Options;
using Quartz;

namespace dr_mailroom_api.Scheduling;

/// <summary>
/// Purga delle mail cestinate oltre la ritenzione (Mailroom:Retention): una DELETE in blocco, le righe collegate
/// seguono in cascata; poi cancella dal disco i .eml archiviati di quelle mail. Un'esecuzione alla volta; non rilancia mai.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PurgeTrashedMessagesJob(
    MessageProvider messageProvider,
    ArchiveProvider archiveProvider,
    IOptions<RetentionOptions> retentionOptions,
    IOptions<AttachmentsOptions> attachmentsOptions,
    ILogger<PurgeTrashedMessagesJob> logger) : IJob
{
    public static readonly JobKey Key = new("purge-trashed-messages", "mailroom");

    /// <summary>
    /// Elimina le righe cestinate da più di TrashedMessagesDays giorni e i loro contenuti archiviati su disco.
    /// I percorsi si leggono prima della DELETE (dopo le righe non ci sono più); i file si cancellano solo a DELETE riuscita.
    /// Gli errori si loggano e si riprova al prossimo scatto.
    /// </summary>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        var days = retentionOptions.Value.TrashedMessagesDays;
        var threshold = DateTime.UtcNow.AddDays(-days);
        try
        {
            var archivedPaths = await archiveProvider.GetPathsOfTrashedBeforeAsync(threshold, cancellationToken);
            var purged = await messageProvider.PurgeTrashedBeforeAsync(threshold, cancellationToken);
            var deletedFiles = archiveProvider.DeleteFiles(attachmentsOptions.Value.RootPath, archivedPaths);
            logger.LogInformation(
                "Purga delle mail cestinate da oltre {Days} giorni: {Purged} righe eliminate, {DeletedFiles} contenuti archiviati cancellati dal disco",
                days, purged, deletedFiles);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Purga delle mail cestinate fallita: si riprova al prossimo scatto del cron");
        }
    }
}
