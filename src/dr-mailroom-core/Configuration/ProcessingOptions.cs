using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Parallelismo e perimetro di lettura dei giri.</summary>
public sealed class ProcessingOptions
{
    public const string SectionName = "Mailroom:Processing";

    /// <summary>Numero massimo di caselle lavorate in parallelo nel giro schedulato.</summary>
    [Range(1, 64)]
    public int MaxParallelMailboxes { get; init; } = 2;

    /// <summary>Consumatori della coda di elaborazione (filtri e processi) per casella; Gmail si legge comunque una mail per volta.</summary>
    [Range(1, 64)]
    public int MaxParallelMessages { get; init; } = 4;

    /// <summary>Se true legge anche Spam e Cestino (D6: default escluso).</summary>
    public bool IncludeSpamTrash { get; init; }
}
