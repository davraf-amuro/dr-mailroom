using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Parametri del motore dei filtri.</summary>
public sealed class FilteringOptions
{
    public const string SectionName = "Mailroom:Filtering";

    /// <summary>Timeout in millisecondi per la valutazione di una regex sull'oggetto (D8).</summary>
    [Range(10, 10_000)]
    public int RegexTimeoutMs { get; init; } = 250;
}
