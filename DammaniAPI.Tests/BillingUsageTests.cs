using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using Xunit;

namespace DammaniAPI.Tests;

public class BillingUsageTests
{
    [Theory]
    [InlineData(0, 30, 0)]
    [InlineData(24, 30, 80)]
    [InlineData(25, 30, 83)]
    [InlineData(30, 30, 100)]
    [InlineData(10, 0, 100)]
    public void ComputePercent_MatchesThresholds(int used, int limit, int expected)
        => Assert.Equal(expected, UsageService.ComputePercent(used, limit));

    [Theory]
    [InlineData(0, 30, 0, "normal")]
    [InlineData(23, 30, 76, "normal")]
    [InlineData(24, 30, 80, "warning")]
    [InlineData(29, 30, 96, "warning")]
    [InlineData(30, 30, 100, "blocked")]
    [InlineData(31, 30, 103, "blocked")]
    public void ComputeLevel_MatchesThresholds(int used, int limit, int percent, string expected)
        => Assert.Equal(expected, UsageService.ComputeLevel(used, limit, percent));

    [Theory]
    [InlineData(WarrantyStatuses.Draft, false)]
    [InlineData(WarrantyStatuses.Active, true)]
    [InlineData(WarrantyStatuses.Cancelled, true)]
    public void CountsTowardUsage_ExcludesDraftsOnly(string status, bool expected)
        => Assert.Equal(expected, UsageService.CountsTowardUsage(status));

    [Fact]
    public void GetUtcMonthPeriod_ReturnsExclusiveEnd()
    {
        var (start, end) = UsageService.GetUtcMonthPeriod(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }
}
