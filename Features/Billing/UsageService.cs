using System.Data;
using Dapper;
using DammaniAPI.Features.Warranties;

namespace DammaniAPI.Features.Billing;

public static class UsageService
{
    public class Usage
    {
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Percent { get; set; }
        public string Level { get; set; } = "normal";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public bool Blocked => Level == "blocked";
    }

    // NEVER block public pages, service requests, viewing/search — only new non-draft card creation.
    // Calendar month UTC for MVP.

    private const int FallbackMonthlyLimit = 30;

    public static async Task<Usage> GetUsageAsync(IDbConnection db, IDbTransaction? tx, string shopId)
    {
        var limit = await db.ExecuteScalarAsync<int?>(
            """
            SELECT p.MonthlyCardLimit
            FROM Subscription sub
            JOIN Plan p ON p.Id = sub.PlanId
            WHERE sub.ShopId = @ShopId
            """,
            new { ShopId = shopId },
            tx);

        // Cancelled cards still count: they consumed a card this month. Drafts excluded.
        var used = await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM Warranty
            WHERE ShopId = @ShopId
              AND Status <> @Draft
              AND CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')
            """,
            new { ShopId = shopId, Draft = WarrantyStatuses.Draft },
            tx);

        var (periodStart, periodEnd) = GetUtcMonthPeriod(DateTime.UtcNow);
        var resolvedLimit = limit ?? FallbackMonthlyLimit;
        var percent = ComputePercent(used, resolvedLimit);
        var level = ComputeLevel(used, resolvedLimit, percent);

        return new Usage
        {
            Used = used,
            Limit = resolvedLimit,
            Percent = percent,
            Level = level,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        };
    }

    internal static int ComputePercent(int used, int limit)
        => limit == 0 ? 100 : used * 100 / limit;

    internal static string ComputeLevel(int used, int limit, int percent)
    {
        if (used >= limit)
            return "blocked";
        if (percent >= 80)
            return "warning";
        return "normal";
    }

    internal static bool CountsTowardUsage(string status)
        => status != WarrantyStatuses.Draft;

    internal static (DateTime PeriodStart, DateTime PeriodEnd) GetUtcMonthPeriod(DateTime utcNow)
    {
        var start = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }
}
