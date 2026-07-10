using DammaniAPI.Utilities;
using Xunit;

namespace DammaniAPI.Tests;

public class UtcDateTimeHandlerTests
{
    private readonly UtcDateTimeHandler _handler = new();

    [Fact]
    public void Parse_AcceptsDateTime()
    {
        var raw = new DateTime(2026, 7, 10, 11, 43, 11, DateTimeKind.Unspecified);
        var parsed = _handler.Parse(raw);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(raw.Ticks, parsed.Ticks);
    }

    [Fact]
    public void Parse_AcceptsMySqlString()
    {
        var parsed = _handler.Parse("2026-07-10 11:43:11");
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(2026, parsed.Year);
        Assert.Equal(7, parsed.Month);
        Assert.Equal(10, parsed.Day);
        Assert.Equal(11, parsed.Hour);
        Assert.Equal(43, parsed.Minute);
    }
}
