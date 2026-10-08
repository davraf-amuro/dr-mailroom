using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Pianificazione del giro automatico su tutte le caselle attive.</summary>
public sealed class ScheduleOptions
{
    public const string SectionName = "Mailroom:Schedule";

    /// <summary>Espressione cron in formato Quartz (secondi inclusi), es. "0 0/15 * * * ?".</summary>
    [Required]
    public string Cron { get; init; } = string.Empty;
}
