using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Features.Dashboard;
using DammaniAPI.Features.Warranties;
using Xunit;

namespace DammaniAPI.Tests;

public class DashboardSummaryTests
{
  private static readonly DateTime July2026 = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime July1 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime June30 = new(2026, 6, 30, 23, 59, 0, DateTimeKind.Utc);
  private static readonly DateTime Today = new(2026, 7, 10);

  [Fact]
  public void WarrantiesThisMonth_ExcludesDraftsAndPriorMonth()
  {
    Assert.True(GetSummary.MetricRules.CountsAsWarrantyThisMonth(July1, WarrantyStatuses.Active, July2026));
    Assert.False(GetSummary.MetricRules.CountsAsWarrantyThisMonth(June30, WarrantyStatuses.Active, July2026));
    Assert.False(GetSummary.MetricRules.CountsAsWarrantyThisMonth(July1, WarrantyStatuses.Draft, July2026));
  }

  [Fact]
  public void ActiveWarranty_RequiresActiveStatusAndNonExpiredExpiry()
  {
    Assert.True(GetSummary.MetricRules.IsActiveWarranty(WarrantyStatuses.Active, Today.AddDays(1), Today));
    Assert.False(GetSummary.MetricRules.IsActiveWarranty(WarrantyStatuses.Active, Today.AddDays(-1), Today));
    Assert.False(GetSummary.MetricRules.IsActiveWarranty(WarrantyStatuses.Active, null, Today));
    Assert.False(GetSummary.MetricRules.IsActiveWarranty(WarrantyStatuses.Cancelled, Today.AddDays(30), Today));
  }

  [Fact]
  public void ExpiringSoon_Includes30DayBoundary()
  {
    Assert.True(GetSummary.MetricRules.IsExpiringSoon(WarrantyStatuses.Active, Today, Today));
    Assert.True(GetSummary.MetricRules.IsExpiringSoon(WarrantyStatuses.Active, Today.AddDays(30), Today));
    Assert.False(GetSummary.MetricRules.IsExpiringSoon(WarrantyStatuses.Active, Today.AddDays(31), Today));
    Assert.False(GetSummary.MetricRules.IsExpiringSoon(WarrantyStatuses.Active, Today.AddDays(-1), Today));
  }

  [Theory]
  [InlineData(ServiceRequestStatuses.New, true)]
  [InlineData(ServiceRequestStatuses.Reviewing, true)]
  [InlineData(ServiceRequestStatuses.Closed, false)]
  public void OpenRequest_ExcludesClosedOnly(string status, bool expected)
    => Assert.Equal(expected, GetSummary.MetricRules.IsOpenRequest(status));

  [Fact]
  public void UsageSummary_MatchesBillingServiceForSameCounts()
  {
    var used = 24;
    var limit = 30;
    var percent = UsageService.ComputePercent(used, limit);
    var level = UsageService.ComputeLevel(used, limit, percent);

    Assert.Equal(80, percent);
    Assert.Equal("warning", level);
  }
}

public class LogShareValidatorTests
{
  [Fact]
  public void RequiresWarrantyId()
  {
    var result = new LogShare.CommandValidator().Validate(new LogShare.Command());

    Assert.Contains(result.Errors, e => e.PropertyName == nameof(LogShare.Command.WarrantyId));
  }
}
