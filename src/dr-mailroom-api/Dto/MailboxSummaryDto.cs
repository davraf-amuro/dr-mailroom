using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Casella in forma ridotta: identità e stato.</summary>
public record MailboxSummaryDto(int Id, string EmailAddress, MailboxStatus Status)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Mailbox, MailboxSummaryDto>> Projection =>
        e => new(e.Id, e.EmailAddress, e.Status);
}
