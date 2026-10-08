using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Credenziali del client OAuth Google; il secret arriva solo da appsettings.local.json o ambiente (C10).</summary>
public sealed class GmailOptions
{
    public const string SectionName = "Mailroom:Gmail";

    /// <summary>ID client OAuth dell'applicazione web registrata in Google Cloud.</summary>
    [Required]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Client secret OAuth: mai in file tracciati.</summary>
    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Redirect URI registrato in Google Console (C2).</summary>
    [Required, Url]
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Unità di quota Gmail consumabili al minuto per casella; Google ne concede 15000 per utente.</summary>
    [Range(600, 15000)]
    public int QuotaUnitsPerMinute { get; init; } = 12000;
}
