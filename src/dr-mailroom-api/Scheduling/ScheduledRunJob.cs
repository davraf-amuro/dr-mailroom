using dr_mailroom_core.Runs;
using Quartz;

namespace dr_mailroom_api.Scheduling;

/// <summary>Job del cron (T7): un'esecuzione alla volta; lavora tutte le caselle attive. Non rilancia mai.</summary>
[DisallowConcurrentExecution]
public sealed class ScheduledRunJob(ScheduledRunner runner, ILogger<ScheduledRunJob> logger) : IJob
{
    public static readonly JobKey Key = new("scheduled-run", "mailroom");

    /// <summary>Esegue il giro schedulato; gli errori si loggano e si riprova al prossimo scatto del cron.</summary>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await runner.RunAllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Giro schedulato fallito: si riprova al prossimo scatto del cron");
        }
    }
}
