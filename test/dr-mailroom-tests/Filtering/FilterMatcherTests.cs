using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dr_mailroom_tests.Filtering;

/// <summary>Verifica mittente (D7), oggetto StartsWith/Contains/Regex (D8), timeout regex e unione dei processi.</summary>
public class FilterMatcherTests
{
    private const string Sender = "fatture@fornitore.it";

    private static FilterMatcher NewMatcher(int regexTimeoutMs = 250) =>
        new(Options.Create(new FilteringOptions { RegexTimeoutMs = regexTimeoutMs }), NullLogger<FilterMatcher>.Instance);

    private static Message NewMessage(string? subject, string sender = Sender) =>
        new() { Id = 1, MailboxId = 1, SenderAddress = sender, Subject = subject };

    private static Filter NewFilter(
        int id,
        SubjectMatchType? type = null,
        string? value = null,
        string sender = Sender,
        params ProcessType[] processes) =>
        new()
        {
            Id = id,
            Name = $"filtro {id}",
            SenderAddress = sender,
            SubjectMatchType = type,
            SubjectValue = value,
            AttachmentSubfolder = $"sub{id}",
            Processes = (processes.Length == 0 ? [ProcessType.Alert] : processes)
                .Select(p => new FilterProcess { FilterId = id, ProcessType = p }).ToList(),
        };

    [Fact]
    public void DifferentSender_DoesNotMatch()
    {
        var result = NewMatcher().Match(NewMessage("Fattura"), [NewFilter(1, sender: "altro@fornitore.it")]);

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void Sender_IsCaseInsensitive_AndFilterWithoutSubjectMatchesAnySubject()
    {
        var result = NewMatcher().Match(NewMessage(null), [NewFilter(1, sender: "Fatture@Fornitore.IT")]);

        Assert.True(result.IsMatch);
    }

    [Fact]
    public void MissingSubject_WithSubjectCondition_DoesNotMatch()
    {
        var result = NewMatcher().Match(NewMessage(null), [NewFilter(1, SubjectMatchType.Contains, "fattura")]);

        Assert.False(result.IsMatch);
    }

    [Theory]
    [InlineData(SubjectMatchType.StartsWith, "fattura", "FATTURA n. 42", true)]
    [InlineData(SubjectMatchType.StartsWith, "fattura", "Invio fattura n. 42", false)]
    [InlineData(SubjectMatchType.Contains, "PAGAMENTO", "Sollecito pagamento scaduto", true)]
    [InlineData(SubjectMatchType.Contains, "pagamento", "Newsletter di ottobre", false)]
    [InlineData(SubjectMatchType.Regex, @"^fattura\s+n\.\s*\d+$", "Fattura n. 2026", true)]
    [InlineData(SubjectMatchType.Regex, @"^fattura\s+n\.\s*\d+$", "Fattura n. duemila", false)]
    public void SubjectConditions_AreEvaluated(SubjectMatchType type, string value, string subject, bool expected)
    {
        var result = NewMatcher().Match(NewMessage(subject), [NewFilter(1, type, value)]);

        Assert.Equal(expected, result.IsMatch);
    }

    [Fact]
    public void RegexTimeout_IsNoMatch_WithoutException()
    {
        // Backtracking catastrofico: (a+)+$ su "aaaa…!" esplode ben oltre 10 ms
        var subject = new string('a', 5_000) + "!";

        var result = NewMatcher(regexTimeoutMs: 10).Match(NewMessage(subject), [NewFilter(1, SubjectMatchType.Regex, "^(a+)+$")]);

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void InvalidRegex_IsNoMatch_WithoutException()
    {
        var result = NewMatcher().Match(NewMessage("Fattura"), [NewFilter(1, SubjectMatchType.Regex, "([unclosed")]);

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void TwoFiltersWithOverlappingProcesses_AreDeduplicated_AndOrderedById()
    {
        var filters = new[]
        {
            NewFilter(7, SubjectMatchType.Contains, "fattura", processes: [ProcessType.AttachmentExtraction, ProcessType.Alert]),
            NewFilter(3, SubjectMatchType.StartsWith, "fattura", processes: [ProcessType.Alert]),
        };

        var result = NewMatcher().Match(NewMessage("Fattura di ottobre"), filters);

        Assert.Equal([3, 7], result.MatchedFilters.Select(f => f.Id));
        Assert.Equal(2, result.Processes.Count);
        Assert.Contains(ProcessType.Alert, result.Processes);
        Assert.Contains(ProcessType.AttachmentExtraction, result.Processes);
        Assert.Equal("sub7", result.AttachmentSubfolder);
    }

    [Fact]
    public void DisabledFilter_OrOtherMailbox_IsIgnored()
    {
        var disabled = NewFilter(1);
        disabled.IsEnabled = false;
        var otherMailbox = NewFilter(2);
        otherMailbox.MailboxId = 99;

        var result = NewMatcher().Match(NewMessage("x"), [disabled, otherMailbox]);

        Assert.False(result.IsMatch);
    }
}
