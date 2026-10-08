using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Totale mail di un mittente in una casella (vista keyless: un solo DTO).</summary>
public record SenderStatDto(int MailboxId, string SenderAddress, int MailCount)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<SenderStat, SenderStatDto>> Projection =>
        e => new(e.MailboxId, e.SenderAddress, e.MailCount);
}
