using dr_mailroom_core.Runs;
using Quartz;

namespace dr_mailroom_api.Scheduling;

/// <summary>IRunDispatcher su Quartz: TriggerJob immediato del job on-demand con l'Id del giro (T7).</summary>
public sealed class QuartzRunDispatcher(ISchedulerFactory schedulerFactory) : IRunDispatcher
{
    /// <summary>Accoda subito il giro; ritorna senza attenderne la fine.</summary>
    public async Task DispatchAsync(long runId, CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var data = new JobDataMap { { OnDemandRunJob.RunIdKey, runId } };

        // Avvio il job on-demand in Quartz
        await scheduler.TriggerJob(OnDemandRunJob.Key, data, cancellationToken);
    }
}
