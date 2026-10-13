using dr_mailroom_api.Dto;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Services;

/// <summary>Registrazione DI di servizi applicativi e validatori dell'API.</summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>Servizi scoped (uno per richiesta) e validatori di body e filtri.</summary>
    public static IServiceCollection AddMailroomApiServices(this IServiceCollection services)
    {
        services.AddScoped<MailboxService>();
        services.AddScoped<FilterService>();
        services.AddScoped<RunService>();
        services.AddScoped<AlertService>();
        services.AddScoped<StatsService>();
        services.AddScoped<MessageService>();

        services.AddScoped<IValidator<FilterRequest>, FilterRequestValidator>();
        services.AddScoped<IValidator<FilterPreviewRequest>, FilterPreviewRequestValidator>();
        services.AddScoped<IValidator<ReapplyRequest>, ReapplyRequestValidator>();
        services.AddScoped<IValidator<StartRunRequest>, StartRunRequestValidator>();
        services.AddScoped<IValidator<TrashSenderRequest>, TrashSenderRequestValidator>();
        services.AddScoped<IValidator<MailboxFilter>, MailboxFilterValidator>();
        services.AddScoped<IValidator<FilterListFilter>, FilterListFilterValidator>();
        services.AddScoped<IValidator<RunFilter>, RunFilterValidator>();
        services.AddScoped<IValidator<AlertListFilter>, AlertListFilterValidator>();
        services.AddScoped<IValidator<SenderStatFilter>, SenderStatFilterValidator>();
        services.AddScoped<IValidator<SenderMonthlyStatFilter>, SenderMonthlyStatFilterValidator>();
        services.AddScoped<IValidator<SenderOverviewFilter>, SenderOverviewFilterValidator>();
        services.AddScoped<IValidator<YearStatFilter>, YearStatFilterValidator>();
        services.AddScoped<IValidator<SenderMessageFilter>, SenderMessageFilterValidator>();
        return services;
    }
}
