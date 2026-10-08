using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Filtri facoltativi sull'elenco dei filtri di lavorazione (nome diverso dall'entità Filter). Tutti i campi sono facoltativi.</summary>
public class FilterListFilter
{
    public string? Name { get; set; }

    public string? SenderAddress { get; set; }

    public int? MailboxId { get; set; }

    public bool? IsEnabled { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati.</summary>
    public Expression<Func<Filter, bool>> ToExpression() =>
        e => (Name == null || e.Name.Contains(Name))
          && (SenderAddress == null || e.SenderAddress.Contains(SenderAddress))
          && (MailboxId == null || e.MailboxId == MailboxId)
          && (IsEnabled == null || e.IsEnabled == IsEnabled);
}
