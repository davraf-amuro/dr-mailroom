namespace dr_mailroom_core.Enums;

/// <summary>Origine di un giro: cron, richiesta manuale, riapplica di un filtro o cestino di un mittente.</summary>
public enum RunTrigger
{
    Scheduled,
    Manual,
    Reapply,
    TrashSender,
}
