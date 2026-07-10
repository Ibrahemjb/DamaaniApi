using DammaniAPI.Features.Billing;
using Xunit;

namespace DammaniAPI.Tests;

public class SubscriptionRollerTests
{
    private static readonly DateTime July31 = new(2026, 7, 31);
    private static readonly DateTime Aug1 = new(2026, 8, 1);
    private static readonly DateTime July15 = new(2026, 7, 15);

    [Fact]
    public void PeriodEnded_IsExclusiveAfterLastDay()
    {
        Assert.False(SubscriptionRoller.PeriodEnded(July31, July31));
        Assert.False(SubscriptionRoller.PeriodEnded(July31, July15));
        Assert.True(SubscriptionRoller.PeriodEnded(July31, Aug1));
    }

    [Fact]
    public void PeriodForDate_MatchesCalendarMonth()
    {
        var (start, end) = SubscriptionRoller.PeriodForDate(new DateTime(2026, 8, 10));

        Assert.Equal(new DateTime(2026, 8, 1), start);
        Assert.Equal(new DateTime(2026, 8, 31), end);
    }
}

public class BillingOverviewTests
{
    [Fact]
    public void ResolvePendingChange_PrioritizesCancel()
    {
        var current = new BillingPlan { SortOrder = 3 };
        var scheduled = new BillingPlan { SortOrder = 2 };

        Assert.Equal("cancel", GetBillingOverview.QueryHandler.ResolvePendingChange(true, current, scheduled));
    }

    [Theory]
    [InlineData(1, 3, "upgrade")]
    [InlineData(3, 1, "downgrade")]
    [InlineData(2, 2, "none")]
    public void ResolvePendingChange_ComparesSortOrder(int currentOrder, int scheduledOrder, string expected)
    {
        var current = new BillingPlan { SortOrder = currentOrder };
        var scheduled = new BillingPlan { SortOrder = scheduledOrder };

        Assert.Equal(expected, GetBillingOverview.QueryHandler.ResolvePendingChange(false, current, scheduled));
    }
}

public class CancelSubscriptionValidatorTests
{
    [Theory]
    [InlineData("too_expensive", true)]
    [InlineData("not_using", true)]
    [InlineData("missing_feature", true)]
    [InlineData("other", true)]
    [InlineData("invalid", false)]
    public void ReasonCode_MustBeSupported(string code, bool valid)
    {
        var result = new CancelSubscription.CommandValidator().Validate(
            new CancelSubscription.Command { ReasonCode = code });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void BuildCancelReason_AppendsNote()
    {
        Assert.Equal("too_expensive", CancelSubscription.CommandHandler.BuildCancelReason("too_expensive", null));
        Assert.Equal(
            "other: switching provider",
            CancelSubscription.CommandHandler.BuildCancelReason("other", "switching provider"));
    }
}

public class RequestUpgradeTests
{
    [Fact]
    public void GenerateReferenceCode_HasExpectedPrefixAndLength()
    {
        var code = RequestUpgrade.CommandHandler.GenerateReferenceCode();

        Assert.StartsWith("UP-", code);
        Assert.Equal(9, code.Length);
    }
}
