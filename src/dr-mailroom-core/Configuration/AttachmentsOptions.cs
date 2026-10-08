using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Destinazione e limiti del processo di estrazione allegati (T10).</summary>
public sealed class AttachmentsOptions
{
    public const string SectionName = "Mailroom:Attachments";

    /// <summary>Cartella radice: le sottocartelle dei filtri sono sempre relative a questa.</summary>
    [Required]
    public string RootPath { get; init; } = string.Empty;

    /// <summary>Dimensione massima di un allegato da salvare; oltre viene saltato con motivo.</summary>
    [Range(1, long.MaxValue)]
    public long MaxSizeBytes { get; init; } = 25 * 1024 * 1024;
}
