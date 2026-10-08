using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Giro in forma ridotta.</summary>
public record RunSummaryDto(long Id, int MailboxId, RunTrigger Trigger, RunStatus Status, DateTime StartedAtUtc)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Run, RunSummaryDto>> Projection =>
        e => new(e.Id, e.MailboxId, e.Trigger, e.Status, e.StartedAtUtc);
}
