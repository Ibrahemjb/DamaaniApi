using System.Data;
using Dapper;

namespace DammaniAPI.Features.Billing;

// Lazy period rollover (no cron): called from GetBillingOverview / GetUsage reads.
// At period end: apply CancelAtPeriodEnd → Free, else apply ScheduledPlanId, then advance dates.
public static class SubscriptionRoller
{
    internal const string FreePlanId = "5b2d0a10-0001-4d10-9c5a-000000000001";

    public class SubscriptionRow
    {
        public string Id { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string? ScheduledPlanId { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public string? CancelReason { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
    }

    public static bool PeriodEnded(DateTime periodEndInclusive, DateTime utcToday)
        => utcToday.Date > periodEndInclusive.Date;

    public static (DateTime Start, DateTime End) PeriodForDate(DateTime utcDate)
    {
        var start = new DateTime(utcDate.Year, utcDate.Month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    public static async Task<SubscriptionRow?> LoadAsync(IDbConnection db, IDbTransaction? tx, string shopId)
        => await db.QueryFirstOrDefaultAsync<SubscriptionRow>(
            """
            SELECT Id, PlanId, ScheduledPlanId, CancelAtPeriodEnd, CancelReason,
                   CurrentPeriodStart, CurrentPeriodEnd
            FROM Subscription
            WHERE ShopId = @ShopId
            """,
            new { ShopId = shopId },
            tx);

    public static async Task RollIfNeededAsync(
        IDbConnection db,
        IDbTransaction? tx,
        string shopId,
        DateTime utcNow)
    {
        var sub = await LoadAsync(db, tx, shopId);
        if (sub is null || !PeriodEnded(sub.CurrentPeriodEnd, utcNow))
            return;

        var (newStart, newEnd) = PeriodForDate(utcNow);
        var newPlanId = sub.PlanId;

        if (sub.CancelAtPeriodEnd)
            newPlanId = FreePlanId;
        else if (!string.IsNullOrWhiteSpace(sub.ScheduledPlanId))
            newPlanId = sub.ScheduledPlanId;

        await db.ExecuteAsync(
            """
            UPDATE Subscription
            SET PlanId = @PlanId,
                ScheduledPlanId = NULL,
                CancelAtPeriodEnd = 0,
                CancelReason = NULL,
                CurrentPeriodStart = @CurrentPeriodStart,
                CurrentPeriodEnd = @CurrentPeriodEnd,
                UpdatedAt = UTC_TIMESTAMP()
            WHERE Id = @Id
            """,
            new
            {
                PlanId = newPlanId,
                CurrentPeriodStart = newStart,
                CurrentPeriodEnd = newEnd,
                sub.Id
            },
            tx);
    }
}
