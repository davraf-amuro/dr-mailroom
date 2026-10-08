using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Casella con stato, collegamento e avanzamento del backfill.</summary>
public record MailboxDto(int Id, string EmailAddress, MailboxStatus Status, bool IsConnected, bool BackfillCompleted, bool BackfillInProgress, DateTime? LastRunAtUtc, string? LastError)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Mailbox, MailboxDto>> Projection =>
        e => new(e.Id, e.EmailAddress, e.Status, e.EncryptedRefreshToken != null, e.BackfillCompleted, e.BackfillPageToken != null, e.LastRunAtUtc, e.LastError);
}
