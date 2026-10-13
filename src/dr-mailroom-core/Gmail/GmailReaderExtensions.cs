using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_core.Gmail;

/// <summary>Registrazione DI dei client Gmail: lettura (gmail.readonly) e cestino (gmail.modify, D-T1 bis).</summary>
public static class GmailReaderExtensions
{
    /// <summary>Registra IGmailReader, IGmailTrasher, i ritentativi di produzione e la factory HTTP della libreria Google.</summary>
    public static IServiceCollection AddMailroomGmail(this IServiceCollection services)
    {
        services.AddSingleton(GmailRetrySettings.Default);
        services.AddSingleton<Google.Apis.Http.IHttpClientFactory>(new Google.Apis.Http.HttpClientFactory());
        services.AddSingleton<IGmailReader, GmailReader>();
        services.AddSingleton<IGmailAuthorizer, GmailAuthorizer>();
        services.AddSingleton<IGmailTrasher, GmailTrasher>();
        return services;
    }
}
