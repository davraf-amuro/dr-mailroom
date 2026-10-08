using System.ComponentModel.DataAnnotations.Schema;
using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Infrastructure.Mailroom.Entities;

/// <summary>Processo associato a un filtro: un filtro ne può avere più di uno.</summary>
[Table("FilterProcess")]
public class FilterProcess
{
    [Column("FilterId")]
    public int FilterId { get; set; }

    [Column("ProcessType")]
    public ProcessType ProcessType { get; set; }

    public Filter? Filter { get; set; }
}
