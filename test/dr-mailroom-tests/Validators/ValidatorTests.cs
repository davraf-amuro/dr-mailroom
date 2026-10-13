using dr_mailroom_api.Dto;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_tests.Validators;

/// <summary>Validatori delle mail di un filtro, del download del contenuto e della data limite "ricevute prima di".</summary>
public class ValidatorTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(1, 1)]
    [InlineData(3, 100)]
    public void FilterMessageFilter_ValidPaging_IsValid(int? page, int? pageSize)
    {
        var result = new FilterMessageFilterValidator().Validate(new FilterMessageFilter { Page = page, PageSize = pageSize });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, null, "page")]
    [InlineData(-1, null, "page")]
    [InlineData(null, 0, "pageSize")]
    [InlineData(null, 101, "pageSize")]
    public void FilterMessageFilter_InvalidPaging_ReportsField(int? page, int? pageSize, string field)
    {
        var result = new FilterMessageFilterValidator().Validate(new FilterMessageFilter { Page = page, PageSize = pageSize });

        Assert.False(result.IsValid);
        Assert.Contains(field, result.Errors.Keys);
    }

    [Theory]
    [InlineData("eml")]
    [InlineData("TXT")]
    [InlineData(" Eml ")]
    public void MessageContentQuery_KnownFormat_IsValid(string format)
    {
        var result = new MessageContentQueryValidator().Validate(new MessageContentQuery { Format = format });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pdf")]
    [InlineData("0")]
    [InlineData("1")]
    public void MessageContentQuery_MissingOrUnknownFormat_ReportsFormat(string? format)
    {
        var result = new MessageContentQueryValidator().Validate(new MessageContentQuery { Format = format });

        Assert.False(result.IsValid);
        Assert.Contains("format", result.Errors.Keys);
    }

    [Fact]
    public void ReceivedBefore_NullOrRealDate_IsValid()
    {
        var errors = new Dictionary<string, string[]>();

        ValidationRules.ReceivedBefore(errors, null);
        ValidationRules.ReceivedBefore(errors, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(errors);
    }

    [Fact]
    public void ReceivedBefore_MinValue_ReportsError()
    {
        var errors = new Dictionary<string, string[]>();

        ValidationRules.ReceivedBefore(errors, DateTime.MinValue);

        Assert.Contains("receivedBeforeUtc", errors.Keys);
    }

    [Fact]
    public void TrashSenderRequest_WithMinValueDate_IsInvalid_AndWithRealDateIsValid()
    {
        var validator = new TrashSenderRequestValidator();

        Assert.False(validator.Validate(new TrashSenderRequest(1, "promo@negozio.it", DateTime.MinValue)).IsValid);
        Assert.True(validator.Validate(new TrashSenderRequest(1, "promo@negozio.it", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc))).IsValid);
        Assert.True(validator.Validate(new TrashSenderRequest(1, "promo@negozio.it")).IsValid);
    }

    [Fact]
    public void SenderMessageFilter_WithMinValueDate_IsInvalid()
    {
        var result = new SenderMessageFilterValidator().Validate(new SenderMessageFilter
        {
            MailboxId = 1,
            SenderAddress = "promo@negozio.it",
            ReceivedBeforeUtc = DateTime.MinValue,
        });

        Assert.False(result.IsValid);
        Assert.Contains("receivedBeforeUtc", result.Errors.Keys);
    }
}
