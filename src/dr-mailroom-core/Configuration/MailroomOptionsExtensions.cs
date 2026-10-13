using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace dr_mailroom_core.Configuration;

/// <summary>Registrazione delle opzioni tipizzate con validazione all'avvio.</summary>
public static class MailroomOptionsExtensions
{
    /// <summary>Lega ogni sezione "Mailroom:*" alla sua classe e fa fallire l'avvio se un valore obbligatorio manca o non è valido.</summary>
    public static IServiceCollection AddMailroomOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ScheduleOptions>()
            .Bind(configuration.GetSection(ScheduleOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => IsValidCron(o.Cron), $"{ScheduleOptions.SectionName}:Cron non è un'espressione cron Quartz valida")
            .ValidateOnStart();

        services.AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => IsValidCron(o.PurgeCron), $"{RetentionOptions.SectionName}:PurgeCron non è un'espressione cron Quartz valida")
            .ValidateOnStart();

        AddValidated<ProcessingOptions>(services, configuration, ProcessingOptions.SectionName);
        AddValidated<FilteringOptions>(services, configuration, FilteringOptions.SectionName);
        AddValidated<AttachmentsOptions>(services, configuration, AttachmentsOptions.SectionName);
        AddValidated<TokenProtectionOptions>(services, configuration, TokenProtectionOptions.SectionName);
        AddValidated<GmailOptions>(services, configuration, GmailOptions.SectionName);
        AddValidated<FrontendOptions>(services, configuration, FrontendOptions.SectionName);

        return services;
    }

    /// <summary>True se l'espressione è un cron Quartz valido: il parser segnala gli errori con un'eccezione.</summary>
    private static bool IsValidCron(string cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return false;
        }

        try
        {
            _ = new CronExpression(cron);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Binding + validazione DataAnnotations + controllo all'avvio per una sezione.</summary>
    private static void AddValidated<TOptions>(IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : class =>
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
