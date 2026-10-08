using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Allerta in forma ridotta.</summary>
public record AlertSummaryDto(long Id, string SenderAddress, string? Subject, DateTime ReceivedAtUtc)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Alert, AlertSummaryDto>> Projection =>
        e => new(e.Id, e.SenderAddress, e.Subject, e.ReceivedAtUtc);
}
