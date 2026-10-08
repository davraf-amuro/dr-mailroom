using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Messaggio indicizzato in forma ridotta (anteprima dei filtri).</summary>
public record MessageSummaryDto(long Id, int MailboxId, string SenderAddress, string? Subject, DateTime ReceivedAtUtc, bool HasAttachments)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Message, MessageSummaryDto>> Projection =>
        e => new(e.Id, e.MailboxId, e.SenderAddress, e.Subject, e.ReceivedAtUtc, e.HasAttachments);
}
