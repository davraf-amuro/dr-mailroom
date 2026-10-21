using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>
/// Riga della griglia delle mail di un mittente. Labels: id delle label Gmail separati da virgola (T2), come salvati.
/// GmailMessageId: per il link "Apri in Gmail".
/// </summary>
public record SenderMessageDto(long Id, string GmailMessageId, string? Subject, DateTime ReceivedAtUtc, string? Labels, bool HasAttachments)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Message, SenderMessageDto>> Projection =>
        e => new(e.Id, e.GmailMessageId, e.Subject, e.ReceivedAtUtc, e.Labels, e.HasAttachments);
}
