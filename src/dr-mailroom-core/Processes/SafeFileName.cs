using System.Text;

namespace dr_mailroom_core.Processes;

/// <summary>Nomi e percorsi sicuri per gli allegati: niente path traversal, niente caratteri non validi, mai fuori dalla root (T10).</summary>
public static class SafeFileName
{
    /// <summary>Formato data/ora all'inizio del nome file.</summary>
    public const string TimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>Lunghezza massima del nome file generato, estensione inclusa.</summary>
    public const int MaxFileNameLength = 180;

    /// <summary>Nome usato quando l'originale è vuoto o fatto solo di caratteri non validi.</summary>
    public const string FallbackName = "allegato";

    private static readonly HashSet<string> _windowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly HashSet<char> _invalidChars =
        [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>Nome deterministico "yyyyMMdd-HHmmss_mittente_nomefile" ripulito e accorciato.</summary>
    public static string Build(DateTime receivedAtUtc, string senderAddress, string originalFileName)
    {
        var name = Sanitize(LastSegment(originalFileName));
        var sender = Sanitize(senderAddress);
        var candidate = $"{receivedAtUtc.ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture)}_{sender}_{name}";
        return Shorten(candidate);
    }

    /// <summary>Cartella di destinazione: root + sottocartella relativa; lancia se il risultato esce dalla root.</summary>
    public static string ResolveDirectory(string rootPath, string? subfolder)
    {
        var root = Path.GetFullPath(rootPath);
        if (string.IsNullOrWhiteSpace(subfolder))
        {
            return root;
        }

        if (Path.IsPathRooted(subfolder))
        {
            throw new InvalidOperationException("La sottocartella degli allegati deve essere relativa alla root");
        }

        var target = Path.GetFullPath(Path.Combine(root, subfolder.Replace('\\', '/')));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootWithSeparator, StringComparison.Ordinal) && target != root)
        {
            throw new InvalidOperationException("La sottocartella degli allegati esce dalla root configurata");
        }

        return target;
    }

    /// <summary>Percorso libero nella cartella: se il nome esiste aggiunge _1, _2, … prima dell'estensione.</summary>
    public static string GetAvailablePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var suffix = 1; File.Exists(path); suffix++)
        {
            path = Path.Combine(directory, $"{stem}_{suffix}{extension}");
        }

        return path;
    }

    /// <summary>Ultimo segmento di un percorso, con separatori sia Windows sia Unix.</summary>
    private static string LastSegment(string fileName)
    {
        var normalized = fileName.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    /// <summary>Sostituisce i caratteri non validi con '_', toglie punti e spazi ai bordi, evita i nomi riservati.</summary>
    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(_invalidChars.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        var cleaned = builder.ToString().Trim().Trim('.', ' ');
        if (cleaned.Length == 0 || cleaned.All(c => c == '_'))
        {
            return FallbackName;
        }

        var stem = Path.GetFileNameWithoutExtension(cleaned);
        return _windowsReservedNames.Contains(stem) ? "_" + cleaned : cleaned;
    }

    /// <summary>Accorcia il nome conservando l'estensione.</summary>
    private static string Shorten(string fileName)
    {
        if (fileName.Length <= MaxFileNameLength)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        if (extension.Length > MaxFileNameLength / 4)
        {
            extension = string.Empty;
        }

        return fileName[..(MaxFileNameLength - extension.Length)] + extension;
    }
}
