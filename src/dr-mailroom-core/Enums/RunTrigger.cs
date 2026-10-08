namespace dr_mailroom_core.Enums;

/// <summary>Origine di un giro: cron, richiesta manuale o riapplica di un filtro.</summary>
public enum RunTrigger
{
    Scheduled,
    Manual,
    Reapply,
}
