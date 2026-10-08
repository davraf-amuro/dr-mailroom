using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Registrazione DI del database dr-mailroom.</summary>
public static class MailroomProviderExtensions
{
    /// <summary>Nome della connection string in configurazione (valore reale solo in appsettings.local.json o ambiente).</summary>
    public const string ConnectionStringName = "MailroomDb";

    /// <summary>Registra la factory del DbContext (usata da servizi singleton e job) e il DbContext scoped.</summary>
    public static IServiceCollection AddMailroomProvider(this IServiceCollection services, IConfiguration configuration)
    {
        // AddDbContextFactory registra anche MailroomDbContext come scoped
        services.AddDbContextFactory<MailroomDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString(ConnectionStringName),
                sql => sql.MigrationsAssembly(typeof(MailroomDbContext).Assembly.GetName().Name));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors(true);
        });

        return services;
    }
}
