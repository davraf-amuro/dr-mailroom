using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Ritenzione delle mail spostate nel cestino da dr-mailroom (D-T1 bis): quando le righe lasciano il DB.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Mailroom:Retention";

    /// <summary>Default della purga: ogni giorno alle 03:30.</summary>
    public const string DefaultPurgeCron = "0 30 3 * * ?";

    /// <summary>Giorni dopo la cestinazione oltre i quali le righe si eliminano dal DB (default 180).</summary>
    [Range(1, 3650)]
    public int TrashedMessagesDays { get; init; } = 180;

    /// <summary>Espressione cron Quartz della purga (secondi inclusi).</summary>
    [Required]
    public string PurgeCron { get; init; } = DefaultPurgeCron;
}
