namespace dr_mailroom_core.Helpers;

/// <summary>Adattamento di testi esterni alle lunghezze delle colonne.</summary>
public static class TextLimits
{
    /// <summary>Tronca il testo alla lunghezza massima; null resta null.</summary>
    public static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
