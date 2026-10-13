using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MimeKit.Text;

namespace dr_mailroom_core.Processes;

/// <summary>
/// Testo leggibile di una mail archiviata: intestazioni essenziali e corpo text/plain, oppure l'HTML convertito
/// in testo quando la mail non ha una parte text/plain. Si ricava dal .eml a ogni richiesta, non si salva.
/// </summary>
public static partial class MessageTextExtractor
{
    /// <summary>Formato della data nell'intestazione del testo.</summary>
    public const string DateFormat = "dd/MM/yyyy HH:mm";

    /// <summary>Testo mostrato quando la mail non ha né testo né HTML.</summary>
    public const string EmptyBodyText = "(mail senza testo)";

    /// <summary>Righe vuote consecutive ammesse nel testo ricavato dall'HTML.</summary>
    public const int MaxBlankLines = 1;

    /// <summary>Tag il cui contenuto non è testo da leggere.</summary>
    private static readonly HashSet<HtmlTagId> _skippedTags = [HtmlTagId.Script, HtmlTagId.Style, HtmlTagId.Head, HtmlTagId.Title];

    /// <summary>Tag di blocco: vanno a capo prima e dopo.</summary>
    private static readonly HashSet<HtmlTagId> _blockTags =
    [
        HtmlTagId.P, HtmlTagId.Div, HtmlTagId.TR, HtmlTagId.LI, HtmlTagId.Table, HtmlTagId.UL, HtmlTagId.OL,
        HtmlTagId.H1, HtmlTagId.H2, HtmlTagId.H3, HtmlTagId.H4, HtmlTagId.H5, HtmlTagId.H6,
        HtmlTagId.BlockQuote, HtmlTagId.Pre, HtmlTagId.HR, HtmlTagId.Center,
    ];

    /// <summary>Legge il .eml e restituisce il testo; lancia FormatException se il contenuto non è un messaggio MIME.</summary>
    public static async Task<string> ToTextAsync(Stream eml, CancellationToken cancellationToken)
    {
        var message = await MimeMessage.LoadAsync(eml, cancellationToken);

        var builder = new StringBuilder();
        builder.Append("Da: ").AppendLine(message.From.ToString());
        builder.Append("Oggetto: ").AppendLine(message.Subject);
        builder.Append("Data: ").AppendLine(message.Date.ToString(DateFormat, CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine(BodyText(message));
        return builder.ToString();
    }

    /// <summary>text/plain se presente; altrimenti HTML convertito in testo; altrimenti un segnaposto.</summary>
    private static string BodyText(MimeMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            return message.TextBody.Trim();
        }

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            var text = HtmlToPlainText(message.HtmlBody);
            return text.Length > 0 ? text : EmptyBodyText;
        }

        return EmptyBodyText;
    }

    /// <summary>
    /// Conversione minima HTML → testo: tiene i soli dati di testo (entità già decodificate dal tokenizer),
    /// salta script, stili e head, va a capo su br e sui tag di blocco, compatta spazi e righe vuote.
    /// </summary>
    public static string HtmlToPlainText(string html)
    {
        using var reader = new StringReader(html);
        var tokenizer = new HtmlTokenizer(reader);
        var builder = new StringBuilder();
        var skipDepth = 0;

        while (tokenizer.ReadNextToken(out var token))
        {
            switch (token)
            {
                case HtmlTagToken tag when _skippedTags.Contains(tag.Id):
                    if (!tag.IsEmptyElement)
                    {
                        skipDepth = Math.Max(0, skipDepth + (tag.IsEndTag ? -1 : 1));
                    }

                    break;
                case HtmlTagToken tag when tag.Id == HtmlTagId.Br || _blockTags.Contains(tag.Id):
                    builder.Append('\n');
                    break;
                case HtmlTagToken tag when tag.Id is HtmlTagId.TD or HtmlTagId.TH && !tag.IsEndTag:
                    builder.Append(' ');
                    break;
                case HtmlScriptDataToken:
                    break;
                case HtmlDataToken data when skipDepth == 0:
                    builder.Append(data.Data);
                    break;
            }
        }

        return Normalize(builder.ToString());
    }

    /// <summary>Spazi orizzontali compattati, righe ripulite, al più MaxBlankLines righe vuote di seguito.</summary>
    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Select(line => HorizontalSpaces().Replace(line.Replace(' ', ' '), " ").Trim());

        var result = new StringBuilder();
        var blank = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                blank++;
                if (blank > MaxBlankLines || result.Length == 0)
                {
                    continue;
                }
            }
            else
            {
                blank = 0;
            }

            result.Append(line).Append('\n');
        }

        return result.ToString().Trim();
    }

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex HorizontalSpaces();
}
