using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Mail di un mittente in un mese (YearMonth = yyyyMM; vista keyless: un solo DTO).</summary>
public record SenderMonthlyStatDto(int MailboxId, string SenderAddress, int YearMonth, int MailCount)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<SenderMonthlyStat, SenderMonthlyStatDto>> Projection =>
        e => new(e.MailboxId, e.SenderAddress, e.YearMonth, e.MailCount);
}
