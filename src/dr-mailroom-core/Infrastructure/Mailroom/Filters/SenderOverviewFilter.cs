using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>
/// Panoramica mittenti di una casella: aggregata per indirizzo email (mai per nome), paginata.
/// MailboxId è obbligatorio; gli altri campi hanno un default.
/// </summary>
public class SenderOverviewFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int? MailboxId { get; set; }

    /// <summary>Anno di ricezione; null = tutto lo storico.</summary>
    public int? Year { get; set; }

    /// <summary>Testo cercato nell'indirizzo del mittente (contiene, minuscolo).</summary>
    public string? Search { get; set; }

    public SenderOverviewSortBy? SortBy { get; set; }

    public SortDirection? SortDir { get; set; }

    /// <summary>Pagina da 1.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }
}
