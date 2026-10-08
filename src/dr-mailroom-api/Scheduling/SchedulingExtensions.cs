using dr_mailroom_core.Configuration;
using dr_mailroom_core.Runs;
using Quartz;

namespace dr_mailroom_api.Scheduling;

/// <summary>Registrazione di Quartz: job schedulato da cron e job on-demand durevole.</summary>
public static class SchedulingExtensions
{
    /// <summary>Identità del trigger cron del giro schedulato.</summary>
    public const string ScheduledTriggerName = "scheduled-run-cron";

    /// <summary>Registra i job, il trigger cron (Mailroom:Schedule:Cron), il servizio hosted e il dispatcher dei giri.</summary>
    public static IServiceCollection AddMailroomScheduling(this IServiceCollection services, IConfiguration configuration)
    {
        // Il cron è validato all'avvio da ScheduleOptions; qui serve già per costruire il trigger
        var cron = configuration[$"{ScheduleOptions.SectionName}:{nameof(ScheduleOptions.Cron)}"] ?? string.Empty;

        services.AddQuartz(quartz =>
        {
            quartz.AddJob<ScheduledRunJob>(job => job.WithIdentity(ScheduledRunJob.Key));
            quartz.AddTrigger(trigger => trigger
                .ForJob(ScheduledRunJob.Key)
                .WithIdentity(ScheduledTriggerName)
                .WithSchedule(CronScheduleBuilder.Create(cron)));

            // Durevole e senza trigger: parte solo con TriggerJob dal dispatcher
            quartz.AddJob<OnDemandRunJob>(job => job.WithIdentity(OnDemandRunJob.Key).StoreDurably());
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.AddSingleton<IRunDispatcher, QuartzRunDispatcher>();
        return services;
    }
}
