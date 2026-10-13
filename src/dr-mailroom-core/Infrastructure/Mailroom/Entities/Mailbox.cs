using System.ComponentModel.DataAnnotations.Schema;
using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Casella Gmail collegata: token cifrato, checkpoint di lettura e stato (D1, T3).</summary>
[Table("Mailbox")]
public class Mailbox
{
    [Column("Id")]
    public int Id { get; set; }

    [Column("EmailAddress")]
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>Refresh token OAuth cifrato con Data Protection (T6); null finché la casella non è collegata.</summary>
    [Column("EncryptedRefreshToken")]
    public string? EncryptedRefreshToken { get; set; }

    /// <summary>
    /// Refresh token OAuth con scope gmail.modify, cifrato come il precedente (D-T1 bis): lo usa solo il cestino.
    /// Null finché l'utente non autorizza il cestino della casella; la lettura non lo vede mai.
    /// </summary>
    [Column("EncryptedTrashRefreshToken")]
    public string? EncryptedTrashRefreshToken { get; set; }

    /// <summary>Base dell'incrementale: historyId catturato all'inizio del backfill o dopo l'ultimo giro riuscito.</summary>
    [Column("HistoryId")]
    public ulong? HistoryId { get; set; }

    /// <summary>Pagina successiva del backfill: permette la ripresa dopo un'interruzione (D2).</summary>
    [Column("BackfillPageToken")]
    public string? BackfillPageToken { get; set; }

    [Column("BackfillCompleted")]
    public bool BackfillCompleted { get; set; }

    /// <summary>Totale messaggi della casella secondo Gmail (getProfile), letto all'avvio o alla ripresa del backfill; denominatore dell'avanzamento.</summary>
    [Column("MessagesTotal")]
    public int? MessagesTotal { get; set; }

    [Column("Status")]
    public MailboxStatus Status { get; set; } = MailboxStatus.Active;

    [Column("LastRunAtUtc")]
    public DateTime? LastRunAtUtc { get; set; }

    [Column("LastError")]
    public string? LastError { get; set; }

    /// <summary>Giro che ha registrato LastError; null se l'ultimo giro concluso è andato a buon fine.</summary>
    [Column("LastErrorRunId")]
    public long? LastErrorRunId { get; set; }

    public ICollection<Message> Messages { get; set; } = [];

    public ICollection<Run> Runs { get; set; } = [];

    public ICollection<Filter> Filters { get; set; } = [];
}
