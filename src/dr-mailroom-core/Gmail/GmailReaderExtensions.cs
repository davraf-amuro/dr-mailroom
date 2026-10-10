using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_core.Gmail;

/// <summary>Registrazione DI del client Gmail in sola lettura.</summary>
public static class GmailReaderExtensions
{
    /// <summary>Registra IGmailReader, i ritentativi di produzione e la factory HTTP della libreria Google.</summary>
    public static IServiceCollection AddMailroomGmail(this IServiceCollection services)
    {
        services.AddSingleton(GmailRetrySettings.Default);
        services.AddSingleton<Google.Apis.Http.IHttpClientFactory>(new Google.Apis.Http.HttpClientFactory());
        services.AddSingleton<IGmailReader, GmailReader>();
        services.AddSingleton<IGmailAuthorizer, GmailAuthorizer>();
        return services;
    }
}
