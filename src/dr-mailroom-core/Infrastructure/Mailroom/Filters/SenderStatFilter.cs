using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Filtri facoltativi sulla statistica totale per mittente. Tutti i campi sono facoltativi.</summary>
public class SenderStatFilter
{
    public int? MailboxId { get; set; }

    public string? SenderAddress { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati.</summary>
    public Expression<Func<SenderStat, bool>> ToExpression() =>
        e => (MailboxId == null || e.MailboxId == MailboxId)
          && (SenderAddress == null || e.SenderAddress.Contains(SenderAddress));
}
