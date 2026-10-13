using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Contenuto originale di una mail salvato su disco (.eml) dal processo ContentArchive: uno per messaggio.</summary>
[Table("ArchivedMessageContent")]
public class ArchivedMessageContent
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("MessageId")]
    public long MessageId { get; set; }

    /// <summary>Percorso relativo a Attachments.RootPath, con separatore '/'.</summary>
    [Column("RelativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [Column("SizeBytes")]
    public long SizeBytes { get; set; }

    [Column("Sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [Column("ArchivedAtUtc")]
    public DateTime ArchivedAtUtc { get; set; }

    public Message? Message { get; set; }
}
