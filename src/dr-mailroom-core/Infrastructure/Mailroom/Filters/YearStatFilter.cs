namespace dr_mailroom_core.Infrastructure.Mailroom.Filters;

/// <summary>Mail per anno di una casella; con SenderAddress, del solo mittente (confronto esatto).</summary>
public class YearStatFilter
{
    public int? MailboxId { get; set; }

    public string? SenderAddress { get; set; }
}
