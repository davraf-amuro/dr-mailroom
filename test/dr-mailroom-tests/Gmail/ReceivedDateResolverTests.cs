using dr_mailroom_core.Gmail;

namespace dr_mailroom_tests.Gmail;

/// <summary>Data di ricezione: internalDate, poi header Date, poi ora di lettura; mai 1970-01-01.</summary>
public class ReceivedDateResolverTests
{
    private const string DateHeader = "Mon, 20 Dec 2021 11:48:00 +0100 (CET)";
    private static readonly DateTime _headerUtc = new(2021, 12, 20, 10, 48, 0, DateTimeKind.Utc);
    private static readonly DateTime _nowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PositiveInternalDate_WinsOverHeader()
    {
        var resolved = ReceivedDateResolver.Resolve(1759622400000, DateHeader, _nowUtc);

        Assert.Equal(new DateTime(2025, 10, 5, 0, 0, 0, DateTimeKind.Utc), resolved.ReceivedAtUtc);
        Assert.Equal(ReceivedDateSource.InternalDate, resolved.Source);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void MissingOrInvalidInternalDate_UsesDateHeader(long? internalDate)
    {
        var resolved = ReceivedDateResolver.Resolve(internalDate, DateHeader, _nowUtc);

        Assert.Equal(_headerUtc, resolved.ReceivedAtUtc);
        Assert.Equal(DateTimeKind.Utc, resolved.ReceivedAtUtc.Kind);
        Assert.Equal(ReceivedDateSource.DateHeader, resolved.Source);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non una data")]
    [InlineData("Thu, 01 Jan 1970 00:00:00 +0000")]
    public void NoUsableDate_UsesIngestionTime(string? header)
    {
        var resolved = ReceivedDateResolver.Resolve(null, header, _nowUtc);

        Assert.Equal(_nowUtc, resolved.ReceivedAtUtc);
        Assert.Equal(ReceivedDateSource.IngestionTime, resolved.Source);
    }

    [Theory]
    [InlineData("Mon, 20 Dec 2021 11:48:00 +0100")]
    [InlineData("20 Dec 2021 11:48:00 +0100")]
    [InlineData("Mon, 20 Dec 2021 10:48:00 GMT")]
    [InlineData("Mon, 20 Dec 2021 11:48:00 +0100 (Central European Standard Time)")]
    public void DateHeader_CommonVariants_AreParsed(string header)
    {
        Assert.True(ReceivedDateResolver.TryParseDateHeader(header, out var utc));
        Assert.Equal(_headerUtc, utc);
    }
}
