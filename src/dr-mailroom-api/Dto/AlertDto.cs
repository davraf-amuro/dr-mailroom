using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Allerta per il software esterno, con la casella e i filtri che l'hanno generata.</summary>
public record AlertDto(long Id, long MessageId, int MailboxId, string SenderAddress, string? Subject, DateTime ReceivedAtUtc, DateTime CreatedAtUtc, DateTime? ConsumedAtUtc, List<int> FilterIds)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Alert, AlertDto>> Projection =>
        e => new(e.Id, e.MessageId, e.Message!.MailboxId, e.SenderAddress, e.Subject, e.ReceivedAtUtc, e.CreatedAtUtc, e.ConsumedAtUtc, e.Filters.Select(f => f.FilterId).ToList());
}
