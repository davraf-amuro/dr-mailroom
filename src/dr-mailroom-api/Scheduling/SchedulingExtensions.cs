using dr_mailroom_core.Configuration;
using dr_mailroom_core.Runs;
using Quartz;

namespace dr_mailroom_api.Scheduling;

/// <summary>Registrazione di Quartz: job schedulato da cron, purga delle cestinate da cron e job on-demand durevole.</summary>
public static class SchedulingExtensions
{
    /// <summary>Identità del trigger cron del giro schedulato.</summary>
    public const string ScheduledTriggerName = "scheduled-run-cron";

    /// <summary>Identità del trigger cron della purga delle mail cestinate.</summary>
    public const string PurgeTriggerName = "purge-trashed-messages-cron";

    /// <summary>
    /// Registra i job, i trigger cron (Mailroom:Schedule:Cron, Mailroom:Retention:PurgeCron), il servizio hosted e
    /// il dispatcher dei giri.
    /// </summary>
    public static IServiceCollection AddMailroomScheduling(this IServiceCollection services, IConfiguration configuration)
    {
        // Il cron è validato all'avvio da ScheduleOptions; qui serve già per costruire il trigger
        var cron = configuration[$"{ScheduleOptions.SectionName}:{nameof(ScheduleOptions.Cron)}"] ?? string.Empty;
        var purgeCron = configuration[$"{RetentionOptions.SectionName}:{nameof(RetentionOptions.PurgeCron)}"]
            ?? RetentionOptions.DefaultPurgeCron;

        services.AddQuartz(quartz =>
        {
            quartz.AddJob<ScheduledRunJob>(job => job.WithIdentity(ScheduledRunJob.Key));
            quartz.AddTrigger(trigger => trigger
                .ForJob(ScheduledRunJob.Key)
                .WithIdentity(ScheduledTriggerName)
                .WithSchedule(CronScheduleBuilder.Create(cron)));

            quartz.AddJob<PurgeTrashedMessagesJob>(job => job.WithIdentity(PurgeTrashedMessagesJob.Key));
            quartz.AddTrigger(trigger => trigger
                .ForJob(PurgeTrashedMessagesJob.Key)
                .WithIdentity(PurgeTriggerName)
                .WithSchedule(CronScheduleBuilder.Create(purgeCron)));

            // Durevole e senza trigger: parte solo con TriggerJob dal dispatcher
            quartz.AddJob<OnDemandRunJob>(job => job.WithIdentity(OnDemandRunJob.Key).StoreDurably());
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.AddSingleton<IRunDispatcher, QuartzRunDispatcher>();
        return services;
    }
}
