using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Riga della vista vw_SenderMonthlyStats: mail per mittente, casella e mese (T5).</summary>
public class SenderMonthlyStat
{
    [Column("MailboxId")]
    public int MailboxId { get; set; }

    [Column("SenderAddress")]
    public string SenderAddress { get; set; } = string.Empty;

    /// <summary>Anno e mese come YEAR*100+MONTH, es. 202610.</summary>
    [Column("YearMonth")]
    public int YearMonth { get; set; }

    [Column("MailCount")]
    public int MailCount { get; set; }
}
