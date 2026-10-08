using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Riga della vista vw_SenderStats: totale mail per mittente e casella (T5).</summary>
public class SenderStat
{
    [Column("MailboxId")]
    public int MailboxId { get; set; }

    [Column("SenderAddress")]
    public string SenderAddress { get; set; } = string.Empty;

    [Column("MailCount")]
    public int MailCount { get; set; }
}
