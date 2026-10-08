using System.ComponentModel.DataAnnotations.Schema;
using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Filtro di lavorazione: mittente obbligatorio e al più una condizione sull'oggetto (D4).</summary>
[Table("Filter")]
public class Filter
{
    [Column("Id")]
    public int Id { get; set; }

    /// <summary>Casella a cui si applica; null = tutte le caselle.</summary>
    [Column("MailboxId")]
    public int? MailboxId { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Indirizzo del mittente, confrontato case-insensitive (D7).</summary>
    [Column("SenderAddress")]
    public string SenderAddress { get; set; } = string.Empty;

    /// <summary>Tipo di confronto sull'oggetto; valorizzato se e solo se lo è SubjectValue (CHECK).</summary>
    [Column("SubjectMatchType")]
    public SubjectMatchType? SubjectMatchType { get; set; }

    [Column("SubjectValue")]
    public string? SubjectValue { get; set; }

    /// <summary>Sottocartella degli allegati, relativa a Attachments.RootPath (T10).</summary>
    [Column("AttachmentSubfolder")]
    public string? AttachmentSubfolder { get; set; }

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    public Mailbox? Mailbox { get; set; }

    public ICollection<FilterProcess> Processes { get; set; } = [];
}
