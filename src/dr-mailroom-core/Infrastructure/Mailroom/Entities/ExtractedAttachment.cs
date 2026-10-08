using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Allegato salvato su disco: unico per (messaggio, parte), quindi mai scritto due volte (T10).</summary>
[Table("ExtractedAttachment")]
public class ExtractedAttachment
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("MessageId")]
    public long MessageId { get; set; }

    /// <summary>PartId MIME di Gmail che identifica l'allegato nel messaggio.</summary>
    [Column("PartId")]
    public string PartId { get; set; } = string.Empty;

    [Column("OriginalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Percorso relativo a Attachments.RootPath.</summary>
    [Column("RelativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [Column("SizeBytes")]
    public long SizeBytes { get; set; }

    [Column("Sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [Column("SavedAtUtc")]
    public DateTime SavedAtUtc { get; set; }

    public Message? Message { get; set; }
}
