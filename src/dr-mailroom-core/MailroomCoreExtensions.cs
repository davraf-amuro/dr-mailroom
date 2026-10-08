using dr_mailroom_core.Filtering;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Ingestion;
using dr_mailroom_core.Processes;
using dr_mailroom_core.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_core;

/// <summary>Registrazione DI della logica di dr-mailroom: provider, ingestione, filtri, processi, giri.</summary>
public static class MailroomCoreExtensions
{
    /// <summary>Registra i servizi del dominio. DB, Gmail e protezione token si registrano a parte.</summary>
    public static IServiceCollection AddMailroomCore(this IServiceCollection services)
    {
        services.AddScoped<MailboxProvider>();
        services.AddScoped<MessageProvider>();
        services.AddScoped<RunProvider>();
        services.AddScoped<FilterProvider>();
        services.AddScoped<AlertProvider>();
        services.AddScoped<AttachmentProvider>();
        services.AddScoped<ProcessExecutionProvider>();
        services.AddScoped<StatsProvider>();

        services.AddScoped<MailboxIngestionService>();

        // Singleton: conserva la cache delle regex compilate
        services.AddSingleton<FilterMatcher>();

        services.AddScoped<IMessageProcess, AttachmentExtractionProcess>();
        services.AddScoped<IMessageProcess, AlertProcess>();

        services.AddSingleton<IMailboxLock, MailboxLock>();
        services.AddScoped<MessageProcessingQueue>();
        services.AddScoped<RunOrchestrator>();
        services.AddSingleton<ScheduledRunner>();

        return services;
    }
}
