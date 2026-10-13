using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>
/// Mail di un mittente in una casella, paginate per data di ricezione.
/// MailboxId e SenderAddress sono obbligatori (li impone il validatore); gli altri campi hanno un default.
/// </summary>
public class SenderMessageFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int? MailboxId { get; set; }

    /// <summary>Indirizzo del mittente, confrontato per uguaglianza (salvato sempre minuscolo, D7).</summary>
    public string? SenderAddress { get; set; }

    /// <summary>Testo cercato nell'oggetto (contiene).</summary>
    public string? Search { get; set; }

    /// <summary>Solo le mail ricevute prima di questo istante UTC (estremo escluso): conteggio di "Elimina tutte" fino a una data.</summary>
    public DateTime? ReceivedBeforeUtc { get; set; }

    /// <summary>Verso dell'ordinamento per data di ricezione; default Desc (dalla più recente).</summary>
    public SortDirection? SortDir { get; set; }

    /// <summary>Pagina da 1.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati; le mail spostate nel cestino da dr-mailroom non compaiono.</summary>
    public Expression<Func<Message, bool>> ToExpression() =>
        e => e.TrashedAtUtc == null
          && (MailboxId == null || e.MailboxId == MailboxId)
          && (SenderAddress == null || e.SenderAddress == SenderAddress)
          && (Search == null || (e.Subject != null && e.Subject.Contains(Search)))
          && (ReceivedBeforeUtc == null || e.ReceivedAtUtc < ReceivedBeforeUtc);
}
