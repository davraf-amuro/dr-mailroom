using System.ComponentModel.DataAnnotations.Schema;
using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Giro su una casella: schedulato, manuale, riapplica di un filtro o cestino di un mittente, con contatori di avanzamento.</summary>
[Table("Run")]
public class Run
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("MailboxId")]
    public int MailboxId { get; set; }

    [Column("Trigger")]
    public RunTrigger Trigger { get; set; }

    /// <summary>Filtro riapplicato; valorizzato solo per Trigger = Reapply.</summary>
    [Column("FilterId")]
    public int? FilterId { get; set; }

    /// <summary>Processi richiesti dalla riapplica, separati da virgola; null = tutti quelli del filtro (D13).</summary>
    [Column("RequestedProcesses")]
    public string? RequestedProcesses { get; set; }

    /// <summary>Mittente le cui mail vanno nel cestino; valorizzato solo per Trigger = TrashSender.</summary>
    [Column("SenderAddress")]
    public string? SenderAddress { get; set; }

    /// <summary>Estremo escluso del giro di cestino: solo le mail ricevute prima di questo istante; null = tutte.</summary>
    [Column("TrashReceivedBeforeUtc")]
    public DateTime? TrashReceivedBeforeUtc { get; set; }

    /// <summary>Giro di cestino limitato alle mail senza allegati; false = tutte (anche per gli altri trigger).</summary>
    [Column("TrashWithoutAttachmentsOnly")]
    public bool TrashWithoutAttachmentsOnly { get; set; }

    [Column("Status")]
    public RunStatus Status { get; set; } = RunStatus.Running;

    [Column("StartedAtUtc")]
    public DateTime StartedAtUtc { get; set; }

    [Column("EndedAtUtc")]
    public DateTime? EndedAtUtc { get; set; }

    [Column("MessagesRead")]
    public int MessagesRead { get; set; }

    [Column("MessagesProcessed")]
    public int MessagesProcessed { get; set; }

    [Column("ErrorCount")]
    public int ErrorCount { get; set; }

    [Column("ErrorSummary")]
    public string? ErrorSummary { get; set; }

    public Mailbox? Mailbox { get; set; }

    public Filter? Filter { get; set; }
}
