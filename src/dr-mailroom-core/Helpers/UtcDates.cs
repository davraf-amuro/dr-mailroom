namespace dr_mailroom_core.Helpers;

/// <summary>Istanti in ingresso portati in UTC: il DB salva solo UTC, il binding può consegnare Local o Unspecified.</summary>
public static class UtcDates
{
    /// <summary>Local → convertito in UTC; Unspecified → considerato già UTC; null resta null.</summary>
    public static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { Kind: DateTimeKind.Unspecified } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
        var utc => utc,
    };
}
