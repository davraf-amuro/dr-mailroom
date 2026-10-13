using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>
/// Casella con stato, collegamento e avanzamento del backfill: MessagesIndexed su MessagesTotal (totale Gmail, approssimato),
/// giro in corso (null se nessuno), mail lette nell'ultimo giro concluso, giro che ha prodotto LastError e cestino
/// autorizzato (D-T1 bis: secondo token gmail.modify presente).
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
    int? LastRunMessagesRead,
    string? LastError,
    long? LastErrorRunId,
    bool TrashAuthorized)
{
    /// <summary>
    /// Proiezione EF-traducibile: new-initializer con accesso diretto ai membri; conteggio, giro in corso e
    /// ultimo giro concluso come sottoquery. "Concluso" = Completed o Failed di lettura o riapplica: gli stessi che
    /// aggiornano LastRunAtUtc (il cestino di un mittente non la aggiorna).
    /// </summary>
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
            e.Runs
                .Where(r => (r.Status == RunStatus.Completed || r.Status == RunStatus.Failed)
                    && r.Trigger != RunTrigger.TrashSender)
                .OrderByDescending(r => r.Id)
                .Select(r => (int?)r.MessagesRead)
                .FirstOrDefault(),
            e.LastError,
            e.LastErrorRunId,
            e.EncryptedTrashRefreshToken != null);
}
