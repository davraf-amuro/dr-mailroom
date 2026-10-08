namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Nomi delle viste statistiche: usati dalla mappatura EF e dalla migration che le crea.</summary>
public static class MailroomViews
{
    public const string SenderStats = "vw_SenderStats";
    public const string SenderMonthlyStats = "vw_SenderMonthlyStats";
}
