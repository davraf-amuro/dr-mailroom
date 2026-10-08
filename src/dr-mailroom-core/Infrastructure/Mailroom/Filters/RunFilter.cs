using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Filtri facoltativi sui giri. Tutti i campi sono facoltativi.</summary>
public class RunFilter
{
    public int? MailboxId { get; set; }

    public RunTrigger? Trigger { get; set; }

    public RunStatus? Status { get; set; }

    public DateTime? StartedAtUtcFrom { get; set; }

    public DateTime? StartedAtUtcTo { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati.</summary>
    public Expression<Func<Run, bool>> ToExpression() =>
        e => (MailboxId == null || e.MailboxId == MailboxId)
          && (Trigger == null || e.Trigger == Trigger)
          && (Status == null || e.Status == Status)
          && (StartedAtUtcFrom == null || e.StartedAtUtc >= StartedAtUtcFrom)
          && (StartedAtUtcTo == null || e.StartedAtUtc <= StartedAtUtcTo);
}
