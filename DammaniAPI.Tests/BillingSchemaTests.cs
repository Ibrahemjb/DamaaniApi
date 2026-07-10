using System.Text.RegularExpressions;
using DammaniAPI.Features;
using Xunit;

namespace DammaniAPI.Tests;

// DMN-1001: the test suite has no live MySQL, so the plan seeds and backfill are
// asserted against the migration script itself (applied scripts are immutable per
// the migration rules, so content assertions are stable). Live apply + signup
// subscription creation are covered by manual QA against a local database.
public class BillingSchemaTests
{
    private static readonly string ScriptsPath =
        Path.Combine(AppContext.BaseDirectory, "Database", "Scripts");

    private static string LoadNormalizedScript()
    {
        var path = Path.Combine(ScriptsPath, "00004_plans_subscriptions.sql");
        Assert.True(File.Exists(path), $"Migration script not found: {path}");
        return Regex.Replace(File.ReadAllText(path), @"\s+", " ");
    }

    [Fact]
    public void MigrationScripts_HaveUniqueNumericPrefixes()
    {
        var prefixes = Directory.GetFiles(ScriptsPath, "*.sql")
            .Select(f => Path.GetFileName(f).Split('_')[0])
            .ToList();

        Assert.Equal(prefixes.Count, prefixes.Distinct().Count());
    }

    [Theory]
    // BP §5 verbatim: code, nameEn, priceUsd, priceIls, cards/month, users,
    // branches, export, customTemplates, printableLabels, damaaniBranding, analytics, sortOrder
    [InlineData("free", "Free", "0.00", "0.00", 30, 1, 0, 0, 0, 0, 1, 0, 1)]
    [InlineData("starter", "Starter", "9.00", "35.00", 300, 2, 0, 0, 0, 0, 0, 0, 2)]
    [InlineData("pro", "Pro", "19.00", "75.00", 1500, 5, 1, 1, 1, 1, 0, 0, 3)]
    [InlineData("business", "Business", "39.00", "150.00", 5000, 15, 1, 1, 1, 1, 0, 1, 4)]
    public void PlanSeeds_MatchBusinessPlanSection5(
        string code, string nameEn, string priceUsd, string priceIls, int cardLimit, int maxUsers,
        int hasBranches, int hasExport, int hasCustomTemplates, int hasPrintableLabels,
        int showDamaaniBranding, int hasAnalytics, int sortOrder)
    {
        var script = LoadNormalizedScript();

        var expectedRow =
            $"'{code}', '{nameEn}', " +
            (code == "free" ? "'المجانية', " : $"'{nameEn}', ") +
            $"{priceUsd}, {priceIls}, {cardLimit}, {maxUsers}, " +
            $"{hasBranches}, {hasExport}, {hasCustomTemplates}, {hasPrintableLabels}, " +
            $"{showDamaaniBranding}, {hasAnalytics}, {sortOrder}, 1)";

        Assert.Contains(expectedRow, script);
    }

    [Fact]
    public void PlanCodes_AllHaveSeeds()
    {
        var script = LoadNormalizedScript();

        foreach (var code in new[] { PlanCodes.Free, PlanCodes.Starter, PlanCodes.Pro, PlanCodes.Business })
            Assert.Contains($"'{code}'", script);
    }

    [Fact]
    public void SubscriptionTable_HoldsScheduledChangeContract()
    {
        var script = LoadNormalizedScript();

        // DMN-1004 depends on these: downgrades/cancellations apply at period end.
        Assert.Contains("ScheduledPlanId VARCHAR(36) NULL", script);
        Assert.Contains("CancelAtPeriodEnd TINYINT(1) NOT NULL DEFAULT 0", script);
        Assert.Contains("CancelReason VARCHAR(300) NULL", script);
        Assert.Contains("UX_Subscription_Shop UNIQUE (ShopId)", script);
        Assert.Contains("CurrentPeriodStart DATE NOT NULL", script);
        Assert.Contains("CurrentPeriodEnd DATE NOT NULL", script);
    }

    [Fact]
    public void PaymentTable_SupportsManualAndGatewayHistory()
    {
        var script = LoadNormalizedScript();

        Assert.Contains("Method VARCHAR(20) NOT NULL", script);
        Assert.Contains("Reference VARCHAR(100) NULL", script);
        Assert.Contains("IX_Payment_Shop_PaidAt ON Payment (ShopId, PaidAt)", script);
    }

    [Fact]
    public void Backfill_GivesEveryShopWithoutSubscriptionAFreeOne()
    {
        var script = LoadNormalizedScript();

        var backfill = Regex.Match(
            script,
            @"INSERT INTO Subscription.+?FROM Shop s WHERE NOT EXISTS \(SELECT 1 FROM Subscription sub WHERE sub\.ShopId = s\.Id\)");

        Assert.True(backfill.Success, "Free-subscription backfill statement missing.");
        Assert.Contains("'5b2d0a10-0001-4d10-9c5a-000000000001'", backfill.Value); // Free plan id
        Assert.Contains("'active'", backfill.Value);
    }
}
