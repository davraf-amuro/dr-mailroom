using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Filtering;

/// <summary>
/// Motore dei filtri: decide quali filtri intercettano un messaggio e quali processi applicare.
/// Nessun accesso a DB o rete; l'unico stato è la cache delle regex compilate.
/// </summary>
public class FilterMatcher(IOptions<FilteringOptions> options, ILogger<FilterMatcher> logger)
{
    /// <summary>Opzioni regex di D8: confronto case-insensitive e indipendente dalla cultura.</summary>
    public const RegexOptions SubjectRegexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private readonly ConcurrentDictionary<string, Regex?> _regexCache = new(StringComparer.Ordinal);

    /// <summary>Valuta i filtri attivi della casella del messaggio; restituisce i corrispondenti e l'unione dei processi.</summary>
    public FilterMatchResult Match(Message message, IEnumerable<Filter> filters)
    {
        var matched = filters
            .Where(f => f.IsEnabled && (f.MailboxId is null || f.MailboxId == message.MailboxId))
            .Where(f => SenderMatches(f, message) && SubjectMatches(f, message))
            .OrderBy(f => f.Id)
            .ToList();

        if (matched.Count == 0)
        {
            return FilterMatchResult.None;
        }

        var processes = matched.SelectMany(f => f.Processes).Select(p => p.ProcessType).ToHashSet();
        return new FilterMatchResult(matched, processes);
    }

    /// <summary>D7: uguaglianza case-insensitive sull'indirizzo del mittente.</summary>
    private static bool SenderMatches(Filter filter, Message message) =>
        string.Equals(filter.SenderAddress.Trim(), message.SenderAddress.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>D8: nessuna condizione → vero; con condizione e oggetto assente → falso.</summary>
    private bool SubjectMatches(Filter filter, Message message)
    {
        if (filter.SubjectMatchType is null || filter.SubjectValue is null)
        {
            return true;
        }

        if (message.Subject is null)
        {
            return false;
        }

        return filter.SubjectMatchType switch
        {
            SubjectMatchType.StartsWith => message.Subject.StartsWith(filter.SubjectValue, StringComparison.OrdinalIgnoreCase),
            SubjectMatchType.Contains => message.Subject.Contains(filter.SubjectValue, StringComparison.OrdinalIgnoreCase),
            SubjectMatchType.Regex => RegexMatches(filter, message),
            _ => false,
        };
    }

    /// <summary>Regex con timeout: timeout o pattern non valido → nessun match e warning, mai eccezione.</summary>
    private bool RegexMatches(Filter filter, Message message)
    {
        var regex = _regexCache.GetOrAdd(filter.SubjectValue!, CreateRegex);
        if (regex is null)
        {
            logger.LogWarning("Filtro {FilterId}: regex non valida, nessun match", filter.Id);
            return false;
        }

        try
        {
            return regex.IsMatch(message.Subject!);
        }
        catch (RegexMatchTimeoutException)
        {
            logger.LogWarning(
                "Filtro {FilterId}: regex in timeout ({TimeoutMs} ms) sul messaggio {MessageId}, nessun match",
                filter.Id, options.Value.RegexTimeoutMs, message.Id);
            return false;
        }
    }

    /// <summary>Compila la regex con il timeout configurato; null se il pattern non è valido.</summary>
    private Regex? CreateRegex(string pattern)
    {
        try
        {
            return new Regex(pattern, SubjectRegexOptions, TimeSpan.FromMilliseconds(options.Value.RegexTimeoutMs));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
