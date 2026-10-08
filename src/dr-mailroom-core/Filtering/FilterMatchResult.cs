using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Filtering;

/// <summary>Filtri che intercettano un messaggio (ordinati per Id) e unione deduplicata dei loro processi.</summary>
public sealed record FilterMatchResult(IReadOnlyList<Filter> MatchedFilters, IReadOnlySet<ProcessType> Processes)
{
    /// <summary>Nessun filtro corrisponde.</summary>
    public static FilterMatchResult None { get; } = new([], new HashSet<ProcessType>());

    /// <summary>True se almeno un filtro corrisponde.</summary>
    public bool IsMatch => MatchedFilters.Count > 0;

    /// <summary>Sottocartella allegati: quella del primo filtro (Id più basso) che prevede l'estrazione, null se nessuno.</summary>
    public string? AttachmentSubfolder => MatchedFilters
        .FirstOrDefault(f => f.Processes.Any(p => p.ProcessType == ProcessType.AttachmentExtraction))?
        .AttachmentSubfolder;
}
