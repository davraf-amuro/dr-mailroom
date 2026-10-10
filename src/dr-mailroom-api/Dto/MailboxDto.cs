using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>
/// Casella con stato, collegamento e avanzamento del backfill: MessagesIndexed su MessagesTotal (totale Gmail, approssimato),
/// giro in corso (null se nessuno) e giro che ha prodotto LastError.
/// </summary>
public record MailboxDto(
    int Id,
    string EmailAddress,
    MailboxStatus Status,
    bool IsConnected,
    bool BackfillCompleted,
    bool BackfillInProgress,
    int? MessagesTotal,
    int MessagesIndexed,
    MailboxRunDto? CurrentRun,
    DateTime? LastRunAtUtc,
    string? LastError,
    long? LastErrorRunId)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri; conteggio e giro in corso come sottoquery.</summary>
    public static Expression<Func<Mailbox, MailboxDto>> Projection =>
        e => new(
            e.Id,
            e.EmailAddress,
            e.Status,
            e.EncryptedRefreshToken != null,
            e.BackfillCompleted,
            e.BackfillPageToken != null,
            e.MessagesTotal,
            e.Messages.Count(),
            e.Runs
                .Where(r => r.Status == RunStatus.Running)
                .OrderByDescending(r => r.Id)
                .Select(r => new MailboxRunDto(r.Id, r.StartedAtUtc, r.MessagesRead))
                .FirstOrDefault(),
            e.LastRunAtUtc,
            e.LastError,
            e.LastErrorRunId);
}
