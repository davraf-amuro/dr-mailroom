using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Filtri facoltativi sulle allerte; Consumed=false restituisce quelle ancora da notificare (nome diverso dall'entità AlertFilter). Tutti i campi sono facoltativi.</summary>
public class AlertListFilter
{
    public bool? Consumed { get; set; }

    public int? MailboxId { get; set; }

    public string? SenderAddress { get; set; }

    public DateTime? ReceivedAtUtcFrom { get; set; }

    public DateTime? ReceivedAtUtcTo { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati.</summary>
    public Expression<Func<Alert, bool>> ToExpression() =>
        e => (Consumed == null || (e.ConsumedAtUtc != null) == Consumed)
          && (MailboxId == null || e.Message!.MailboxId == MailboxId)
          && (SenderAddress == null || e.SenderAddress.Contains(SenderAddress))
          && (ReceivedAtUtcFrom == null || e.ReceivedAtUtc >= ReceivedAtUtcFrom)
          && (ReceivedAtUtcTo == null || e.ReceivedAtUtc <= ReceivedAtUtcTo);
}
