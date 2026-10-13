using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>
/// Pagina delle mail che corrispondono a un filtro salvato. La corrispondenza sull'oggetto (anche regex) si valuta
/// in memoria con FilterMatcher, quindi niente WHERE EF: la paginazione avviene dopo il confronto.
/// </summary>
public class FilterMessageFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>Verso dell'ordinamento per data di ricezione; default Desc (dalla più recente).</summary>
    public SortDirection? SortDir { get; set; }

    /// <summary>Pagina da 1.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }
}
