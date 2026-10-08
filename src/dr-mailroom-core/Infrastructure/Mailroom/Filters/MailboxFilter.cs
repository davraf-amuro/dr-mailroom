using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Filtri facoltativi sulle caselle. Tutti i campi sono facoltativi.</summary>
public class MailboxFilter
{
    public string? EmailAddress { get; set; }

    public MailboxStatus? Status { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati.</summary>
    public Expression<Func<Mailbox, bool>> ToExpression() =>
        e => (EmailAddress == null || e.EmailAddress.Contains(EmailAddress))
          && (Status == null || e.Status == Status);
}
