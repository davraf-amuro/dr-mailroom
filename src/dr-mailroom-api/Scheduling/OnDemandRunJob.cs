using dr_mailroom_core.Runs;
using Quartz;

namespace dr_mailroom_api.Scheduling;

/// <summary>Job per giri manuali e riapplica (D11, D13): esegue il giro indicato nel JobDataMap. Non rilancia mai.</summary>
public sealed class OnDemandRunJob(IServiceScopeFactory scopeFactory, ILogger<OnDemandRunJob> logger) : IJob
{
    public static readonly JobKey Key = new("on-demand-run", "mailroom");

    /// <summary>Chiave del JobDataMap con l'Id del giro.</summary>
    public const string RunIdKey = "RunId";

    /// <summary>Esegue il giro in uno scope DI dedicato.</summary>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        var runId = context.MergedJobDataMap.GetLong(RunIdKey);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RunOrchestrator>().ExecuteAsync(runId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Giro on-demand {RunId} non eseguito", runId);
        }
    }
}
