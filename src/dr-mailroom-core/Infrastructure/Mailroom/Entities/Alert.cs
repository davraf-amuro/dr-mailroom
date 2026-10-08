using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Allerta: una sola per messaggio (D3), letta e consumata dal software esterno.</summary>
[Table("Alert")]
public class Alert
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("MessageId")]
    public long MessageId { get; set; }

    [Column("SenderAddress")]
    public string SenderAddress { get; set; } = string.Empty;

    [Column("Subject")]
    public string? Subject { get; set; }

    [Column("ReceivedAtUtc")]
    public DateTime ReceivedAtUtc { get; set; }

    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Valorizzata dal software esterno quando ha notificato l'utente.</summary>
    [Column("ConsumedAtUtc")]
    public DateTime? ConsumedAtUtc { get; set; }

    public Message? Message { get; set; }

    public ICollection<AlertFilter> Filters { get; set; } = [];
}
