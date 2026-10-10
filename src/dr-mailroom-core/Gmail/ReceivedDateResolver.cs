using System.Globalization;
using System.Text.RegularExpressions;

namespace dr_mailroom_core.Gmail;

/// <summary>
/// Data di ricezione di un messaggio senza ripieghi silenziosi su 1970-01-01:
/// internalDate se positivo, altrimenti header Date, altrimenti l'ora di lettura.
/// </summary>
public static partial class ReceivedDateResolver
{
    /// <summary>Data scelta e sua fonte.</summary>
    public readonly record struct ResolvedDate(DateTime ReceivedAtUtc, ReceivedDateSource Source);

    /// <summary>Risolve la data di ricezione in UTC.</summary>
    public static ResolvedDate Resolve(long? internalDateMs, string? dateHeader, DateTime nowUtc)
    {
        if (internalDateMs is > 0)
        {
            return new(DateTimeOffset.FromUnixTimeMilliseconds(internalDateMs.Value).UtcDateTime, ReceivedDateSource.InternalDate);
        }

        if (TryParseDateHeader(dateHeader, out var headerUtc))
        {
            return new(headerUtc, ReceivedDateSource.DateHeader);
        }

        return new(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), ReceivedDateSource.IngestionTime);
    }

    /// <summary>Header Date RFC 5322 (es. "Mon, 20 Dec 2021 11:48:00 +0100 (CET)"): commenti tra parentesi ignorati.</summary>
    public static bool TryParseDateHeader(string? dateHeader, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(dateHeader))
        {
            return false;
        }

        var cleaned = CommentPattern().Replace(dateHeader, string.Empty).Trim();
        if (!DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed)
            || parsed <= DateTimeOffset.UnixEpoch)
        {
            return false;
        }

        utc = parsed.UtcDateTime;
        return true;
    }

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex CommentPattern();
}
