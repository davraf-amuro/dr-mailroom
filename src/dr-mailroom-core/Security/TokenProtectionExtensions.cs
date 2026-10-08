using dr_mailroom_core.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dr_mailroom_core.Security;

/// <summary>Registrazione di Data Protection e del protettore dei refresh token.</summary>
public static class TokenProtectionExtensions
{
    /// <summary>Nome applicativo condiviso dal key ring: chi lo cambia non riesce più a decifrare i token salvati.</summary>
    public const string ApplicationName = "dr-mailroom";

    /// <summary>Key ring persistito su file (volume in container, mai nel DB: T6, C11) + ITokenProtector singleton.</summary>
    public static IServiceCollection AddMailroomTokenProtection(this IServiceCollection services, IConfiguration configuration)
    {
        // Il percorso è letto qui perché Data Protection va configurato prima che le opzioni siano risolte
        var keyRingPath = configuration[$"{TokenProtectionOptions.SectionName}:{nameof(TokenProtectionOptions.KeyRingPath)}"];
        if (string.IsNullOrWhiteSpace(keyRingPath))
        {
            throw new InvalidOperationException($"{TokenProtectionOptions.SectionName}:{nameof(TokenProtectionOptions.KeyRingPath)} è obbligatorio");
        }

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
            .SetApplicationName(ApplicationName);

        services.AddSingleton<ITokenProtector, TokenProtector>();
        services.AddSingleton<OAuthStateProtector>();
        return services;
    }
}
