using System.Net.Mail;
using System.Text.RegularExpressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Infrastructure.Mailroom;

namespace dr_mailroom_api.Validators;

/// <summary>Regole di validazione condivise tra i validatori dei filtri (D15).</summary>
public static class ValidationRules
{
    /// <summary>Timeout usato solo per verificare che una regex compili.</summary>
    private static readonly TimeSpan _compileCheckTimeout = TimeSpan.FromSeconds(1);

    private static readonly char[] _invalidPathChars = [.. Path.GetInvalidPathChars(), ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>Aggiunge un messaggio di errore al campo.</summary>
    public static void Add(IDictionary<string, string[]> errors, string field, string message) =>
        errors[field] = errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];

    /// <summary>Mittente obbligatorio: indirizzo email semplice (senza nome visualizzato), max 320.</summary>
    public static void SenderAddress(IDictionary<string, string[]> errors, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, "senderAddress", "Il mittente è obbligatorio");
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > ColumnLengths.EmailAddress)
        {
            Add(errors, "senderAddress", $"Il mittente supera {ColumnLengths.EmailAddress} caratteri");
        }

        if (!MailAddress.TryCreate(trimmed, out var address) || !string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, "senderAddress", "Il mittente non è un indirizzo email valido");
        }
    }

    /// <summary>Casella facoltativa: se presente, Id positivo (l'esistenza la verifica il servizio).</summary>
    public static void OptionalMailboxId(IDictionary<string, string[]> errors, int? mailboxId)
    {
        if (mailboxId is <= 0)
        {
            Add(errors, "mailboxId", "L'Id della casella deve essere positivo");
        }
    }

    /// <summary>Casella obbligatoria: Id positivo (l'esistenza la verifica il servizio).</summary>
    public static void RequiredMailboxId(IDictionary<string, string[]> errors, int? mailboxId)
    {
        if (mailboxId is null)
        {
            Add(errors, "mailboxId", "La casella è obbligatoria");
            return;
        }

        OptionalMailboxId(errors, mailboxId);
    }

    /// <summary>Primo anno ammesso nei filtri: le date di ricezione non scendono sotto l'epoca Unix.</summary>
    public const int MinYear = 1970;

    /// <summary>Anno facoltativo: da 1970 all'anno corrente + 1.</summary>
    public static void OptionalYear(IDictionary<string, string[]> errors, int? year)
    {
        var maxYear = DateTime.UtcNow.Year + 1;
        if (year is int value && (value < MinYear || value > maxYear))
        {
            Add(errors, "year", $"L'anno va da {MinYear} a {maxYear}");
        }
    }

    /// <summary>Condizione sull'oggetto: tipo e valore insieme o nessuno dei due; tipo ammesso; valore max 500; regex compilabile.</summary>
    public static void SubjectCondition(IDictionary<string, string[]> errors, string? matchType, string? value)
    {
        var hasType = !string.IsNullOrWhiteSpace(matchType);
        var hasValue = !string.IsNullOrEmpty(value);
        if (hasType != hasValue)
        {
            Add(errors, "subjectMatchType", "subjectMatchType e subjectValue vanno indicati insieme oppure omessi entrambi");
            return;
        }

        if (!hasType)
        {
            return;
        }

        if (!Enum.TryParse<SubjectMatchType>(matchType, ignoreCase: false, out var type) || !Enum.IsDefined(type))
        {
            Add(errors, "subjectMatchType", $"Tipo ammesso: {string.Join(", ", Enum.GetNames<SubjectMatchType>())}");
            return;
        }

        if (value!.Length > ColumnLengths.SubjectValue)
        {
            Add(errors, "subjectValue", $"Il valore supera {ColumnLengths.SubjectValue} caratteri");
        }

        if (type == SubjectMatchType.Regex && !IsCompilableRegex(value))
        {
            Add(errors, "subjectValue", "L'espressione regolare non è valida");
        }
    }

    /// <summary>Lista processi: almeno uno (se richiesto), valori ammessi, nessun duplicato.</summary>
    public static IReadOnlyList<ProcessType> Processes(IDictionary<string, string[]> errors, IReadOnlyList<string>? values, bool required)
    {
        if (values is null)
        {
            if (required)
            {
                Add(errors, "processes", "Indicare almeno un processo");
            }

            return [];
        }

        if (values.Count == 0)
        {
            Add(errors, "processes", "Indicare almeno un processo");
            return [];
        }

        var parsed = new List<ProcessType>();
        foreach (var value in values)
        {
            if (Enum.TryParse<ProcessType>(value, ignoreCase: false, out var type) && Enum.IsDefined(type))
            {
                parsed.Add(type);
            }
            else
            {
                Add(errors, "processes", $"Processo '{value}' non ammesso: {string.Join(", ", Enum.GetNames<ProcessType>())}");
            }
        }

        if (parsed.Count != parsed.Distinct().Count())
        {
            Add(errors, "processes", "Processi duplicati");
        }

        return parsed;
    }

    /// <summary>Sottocartella relativa: niente percorso assoluto, niente "..", niente caratteri non validi, max 200.</summary>
    public static void AttachmentSubfolder(IDictionary<string, string[]> errors, string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                Add(errors, "attachmentSubfolder", "La sottocartella è obbligatoria con il processo AttachmentExtraction");
            }

            return;
        }

        if (value.Length > ColumnLengths.Subfolder)
        {
            Add(errors, "attachmentSubfolder", $"La sottocartella supera {ColumnLengths.Subfolder} caratteri");
        }

        var segments = value.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\')
            || segments.Any(s => s == "..") || value.IndexOfAny(_invalidPathChars) >= 0)
        {
            Add(errors, "attachmentSubfolder", "La sottocartella deve essere un percorso relativo senza '..' né caratteri non validi");
        }
    }

    /// <summary>Lunghezza massima di un campo testo facoltativo dei filtri di query.</summary>
    public static void MaxLength(IDictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            Add(errors, field, $"Il campo supera {maxLength} caratteri");
        }
    }

    /// <summary>True se il pattern compila con le stesse opzioni del motore dei filtri.</summary>
    private static bool IsCompilableRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern, FilterMatcher.SubjectRegexOptions, _compileCheckTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
