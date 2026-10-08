using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Filtri facoltativi sulla statistica mensile per mittente; YearMonth nel formato yyyyMM. Tutti i campi sono facoltativi.</summary>
public class SenderMonthlyStatFilter
{
    public int? MailboxId { get; set; }

    public string? SenderAddress { get; set; }

    public int? YearMonthFrom { get; set; }

    public int? YearMonthTo { get; set; }

    /// <summary>WHERE EF-traducibile con i soli filtri valorizzati.</summary>
    public Expression<Func<SenderMonthlyStat, bool>> ToExpression() =>
        e => (MailboxId == null || e.MailboxId == MailboxId)
          && (SenderAddress == null || e.SenderAddress.Contains(SenderAddress))
          && (YearMonthFrom == null || e.YearMonth >= YearMonthFrom)
          && (YearMonthTo == null || e.YearMonth <= YearMonthTo);
}
