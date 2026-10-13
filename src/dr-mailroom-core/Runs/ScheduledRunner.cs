using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Runs;

/// <summary>
/// Giro schedulato: tutte le caselle attive in parallelo (MaxParallelMailboxes), ognuna nel proprio scope DI,
/// così l'errore di una casella non ferma le altre (T9).
/// </summary>
public class ScheduledRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessingOptions> options,
    ILogger<ScheduledRunner> logger)
{
    /// <summary>Crea ed esegue un giro Scheduled per ogni casella attiva.</summary>
    public async Task RunAllAsync(CancellationToken cancellationToken)
    {
        List<int> mailboxIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            // Leggo da DB le caselle attive e collegate
            mailboxIds = await scope.ServiceProvider.GetRequiredService<MailboxProvider>().GetActiveMailboxIdsAsync(cancellationToken);
        }

        logger.LogInformation("Giro schedulato: {MailboxCount} caselle attive", mailboxIds.Count);

        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxParallelMailboxes,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(mailboxIds, parallel, async (mailboxId, ct) =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                // Scrivo su DB il giro schedulato della casella, poi lo eseguo
                var runId = await scope.ServiceProvider.GetRequiredService<RunProvider>()
                    .CreateRunAsync(mailboxId, RunTrigger.Scheduled, null, null, null, ct);
                await scope.ServiceProvider.GetRequiredService<RunOrchestrator>().ExecuteAsync(runId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Giro schedulato della casella {MailboxId} non eseguito", mailboxId);
            }
        });
    }
}
