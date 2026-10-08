using System.ComponentModel.DataAnnotations.Schema;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Collegamento tra un'allerta e ciascun filtro che l'ha generata (D3).</summary>
[Table("AlertFilter")]
public class AlertFilter
{
    [Column("AlertId")]
    public long AlertId { get; set; }

    [Column("FilterId")]
    public int FilterId { get; set; }

    public Alert? Alert { get; set; }

    public Filter? Filter { get; set; }
}
