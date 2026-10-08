using System.ComponentModel.DataAnnotations.Schema;
using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Esito di un processo su un messaggio: unico per (messaggio, processo), garantisce l'idempotenza.</summary>
[Table("ProcessExecution")]
public class ProcessExecution
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("MessageId")]
    public long MessageId { get; set; }

    [Column("ProcessType")]
    public ProcessType ProcessType { get; set; }

    [Column("Status")]
    public ProcessExecutionStatus Status { get; set; }

    [Column("Error")]
    public string? Error { get; set; }

    [Column("ProcessedAtUtc")]
    public DateTime ProcessedAtUtc { get; set; }

    public Message? Message { get; set; }
}
