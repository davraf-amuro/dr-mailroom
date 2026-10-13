using System.Text;
using dr_mailroom_core.Processes;

namespace dr_mailroom_tests.Processes;

/// <summary>Testo leggibile dal .eml: text/plain preferito, HTML convertito senza script né stili, intestazioni in testa.</summary>
public class MessageTextExtractorTests
{
    private const string Headers =
        "From: Negozio <promo@negozio.it>\r\nSubject: =?UTF-8?Q?Il_tuo_codice_=E2=82=AC?=\r\nDate: Thu, 01 Oct 2026 10:30:00 +0200\r\nMIME-Version: 1.0\r\n";

    private static Task<string> ExtractAsync(string eml) =>
        MessageTextExtractor.ToTextAsync(new MemoryStream(Encoding.UTF8.GetBytes(eml)), CancellationToken.None);

    [Fact]
    public async Task PlainText_IsReturnedWithHeadersAsync()
    {
        var text = await ExtractAsync(Headers + "Content-Type: text/plain; charset=utf-8\r\n\r\nCodice: SCONTO10\r\n");

        Assert.Contains("Da: \"Negozio\" <promo@negozio.it>", text, StringComparison.Ordinal);
        Assert.Contains("Oggetto: Il tuo codice €", text, StringComparison.Ordinal);
        Assert.Contains("Data: 01/10/2026 10:30", text, StringComparison.Ordinal);
        Assert.EndsWith("Codice: SCONTO10" + Environment.NewLine, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HtmlOnly_IsConvertedToText_WithoutScriptsAndStylesAsync()
    {
        var html = "<html><head><title>T</title><style>p{color:red}</style></head><body>"
            + "<script>alert(1)</script><p>Ciao,</p><p>usa il codice <b>SCONTO&nbsp;20</b>&amp; risparmia</p>"
            + "<table><tr><td>Valido</td><td>fino al 31/12</td></tr></table></body></html>";

        var text = await ExtractAsync(Headers + "Content-Type: text/html; charset=utf-8\r\n\r\n" + html + "\r\n");

        Assert.Contains("Ciao,", text, StringComparison.Ordinal);
        Assert.Contains("usa il codice SCONTO 20& risparmia", text, StringComparison.Ordinal);
        Assert.Contains("Valido fino al 31/12", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", text, StringComparison.Ordinal);
        Assert.DoesNotContain("color", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultipartAlternative_PrefersPlainTextAsync()
    {
        var body =
            "Content-Type: multipart/alternative; boundary=\"b1\"\r\n\r\n"
            + "--b1\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nVersione testo\r\n"
            + "--b1\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<p>Versione html</p>\r\n"
            + "--b1--\r\n";

        var text = await ExtractAsync(Headers + body);

        Assert.Contains("Versione testo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Versione html", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoBody_UsesPlaceholderAsync()
    {
        var text = await ExtractAsync(Headers + "Content-Type: image/png\r\nContent-Transfer-Encoding: base64\r\n\r\niVBORw0KGgo=\r\n");

        Assert.Contains(MessageTextExtractor.EmptyBodyText, text, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlToPlainText_CollapsesBlankLines()
    {
        var text = MessageTextExtractor.HtmlToPlainText("<div>uno</div><br><br><br><br><div>  due   tre </div>");

        Assert.Equal("uno\n\ndue tre", text);
    }
}
