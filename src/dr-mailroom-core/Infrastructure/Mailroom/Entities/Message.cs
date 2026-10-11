using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Messaggio Gmail indicizzato: base delle statistiche (D9) e dei filtri.</summary>
[Table("Message")]
public class Message
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("MailboxId")]
    public int MailboxId { get; set; }

    /// <summary>Id Gmail del messaggio: unico per casella (T4), mai l'header Message-ID.</summary>
    [Column("GmailMessageId")]
    public string GmailMessageId { get; set; } = string.Empty;

    [Column("ThreadId")]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Indirizzo del mittente, sempre minuscolo (D7).</summary>
    [Column("SenderAddress")]
    public string SenderAddress { get; set; } = string.Empty;

    [Column("SenderName")]
    public string? SenderName { get; set; }

    [Column("Subject")]
    public string? Subject { get; set; }

    /// <summary>Data di ricezione da internalDate di Gmail (T5).</summary>
    [Column("ReceivedAtUtc")]
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary>Colonna calcolata persistita YEAR*100+MONTH (es. 202610) per le statistiche mensili.</summary>
    [Column("YearMonth")]
    public int YearMonth { get; private set; }

    /// <summary>Label Gmail separate da virgola (T2).</summary>
    [Column("Labels")]
    public string? Labels { get; set; }

    [Column("HasAttachments")]
    public bool HasAttachments { get; set; }

    /// <summary>Posta inviata: label SENT oppure mittente uguale all'indirizzo della casella. Solo segnalazione, non esclude nulla.</summary>
    [Column("IsSent")]
    public bool IsSent { get; set; }

    public Mailbox? Mailbox { get; set; }
}
