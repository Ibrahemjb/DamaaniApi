using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Features.Reports;
using DammaniAPI.Features.Warranties;
using Xunit;

namespace DammaniAPI.Tests;

public class ReportsRangeTests
{
    private static readonly DateTime July2026 = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime July1 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Aug1 = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveRange_DefaultsToCurrentUtcMonth()
    {
        var (from, to, error) = GetReports.ResolveRange(null, null, July2026);

        Assert.Null(error);
        Assert.Equal(July1, from);
        Assert.Equal(Aug1, to);
    }

    [Fact]
    public void ResolveRange_RejectsFromAfterTo()
    {
        var (_, _, error) = GetReports.ResolveRange(Aug1, July1, July2026);

        Assert.Equal(ErrorCodes.InvalidRange, error);
    }

    [Fact]
    public void ResolveRange_RejectsPartialInput()
    {
        var (_, _, error) = GetReports.ResolveRange(July1, null, July2026);

        Assert.Equal(ErrorCodes.InvalidRange, error);
    }

    [Fact]
    public void ResolveRange_RejectsMoreThan366Days()
    {
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(367);
        var (_, _, error) = GetReports.ResolveRange(from, to, July2026);

        Assert.Equal(ErrorCodes.InvalidRange, error);
    }

    [Fact]
    public void ResolveRange_AllowsExactly366Days()
    {
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(366);
        var (resolvedFrom, resolvedTo, error) = GetReports.ResolveRange(from, to, July2026);

        Assert.Null(error);
        Assert.Equal(from, resolvedFrom);
        Assert.Equal(to, resolvedTo);
    }
}

public class ReportsMetricTests
{
    private static readonly DateTime July1 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Aug1 = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime June30 = new(2026, 6, 30, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 7, 10);

    [Fact]
    public void WarrantiesInRange_ExcludesDraftsAndOutsideRange()
    {
        Assert.True(GetReports.MetricRules.CountsAsWarrantyInRange(July1, WarrantyStatuses.Active, July1, Aug1));
        Assert.False(GetReports.MetricRules.CountsAsWarrantyInRange(June30, WarrantyStatuses.Active, July1, Aug1));
        Assert.False(GetReports.MetricRules.CountsAsWarrantyInRange(July1, WarrantyStatuses.Draft, July1, Aug1));
        Assert.False(GetReports.MetricRules.CountsAsWarrantyInRange(Aug1, WarrantyStatuses.Active, July1, Aug1));
    }

    [Fact]
    public void ExpiredWarranty_RequiresActiveStatusAndPastExpiry()
    {
        Assert.True(GetReports.MetricRules.IsExpiredWarranty(WarrantyStatuses.Active, Today.AddDays(-1), Today));
        Assert.False(GetReports.MetricRules.IsExpiredWarranty(WarrantyStatuses.Active, Today, Today));
        Assert.False(GetReports.MetricRules.IsExpiredWarranty(WarrantyStatuses.Cancelled, Today.AddDays(-1), Today));
    }

    [Fact]
    public void ActiveWarranty_RequiresFutureOrTodayExpiry()
    {
        Assert.True(GetReports.MetricRules.IsActiveWarranty(WarrantyStatuses.Active, Today, Today));
        Assert.False(GetReports.MetricRules.IsActiveWarranty(WarrantyStatuses.Active, Today.AddDays(-1), Today));
    }

    [Fact]
    public void ExpiringSoon_Includes30DayBoundary()
    {
        Assert.True(GetReports.MetricRules.IsExpiringSoon(WarrantyStatuses.Active, Today.AddDays(30), Today));
        Assert.False(GetReports.MetricRules.IsExpiringSoon(WarrantyStatuses.Active, Today.AddDays(31), Today));
    }

    [Theory]
    [InlineData(ServiceRequestStatuses.New, true)]
    [InlineData(ServiceRequestStatuses.Closed, false)]
    public void OpenRequest_ExcludesClosedOnly(string status, bool expected)
        => Assert.Equal(expected, GetReports.MetricRules.IsOpenRequest(status));

    [Fact]
    public void RequestsInRange_UsesExclusiveEnd()
    {
        Assert.True(GetReports.MetricRules.CountsAsRequestInRange(July1, July1, Aug1));
        Assert.False(GetReports.MetricRules.CountsAsRequestInRange(Aug1, July1, Aug1));
    }

    [Fact]
    public void EmptyResult_HasZeroedStructures()
    {
        var result = new GetReports.Result { Success = true };

        Assert.NotNull(result.Cards);
        Assert.Equal(0, result.Cards.WarrantiesInRange);
        Assert.Empty(result.WarrantiesByCategory);
        Assert.Empty(result.RequestsByStatus);
        Assert.Empty(result.ExpiringSoon);
        Assert.Empty(result.CommonProblemTypes);
        Assert.Empty(result.RepeatServiceProducts);
    }

    [Fact]
    public void DefaultMonthPeriod_MatchesUsageService()
    {
        var utcNow = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var reports = GetReports.ResolveRange(null, null, utcNow);
        var usage = UsageService.GetUtcMonthPeriod(utcNow);

        Assert.Equal(usage.PeriodStart, reports.From);
        Assert.Equal(usage.PeriodEnd, reports.To);
    }
}
