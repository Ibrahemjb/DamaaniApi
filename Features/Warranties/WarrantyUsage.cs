using System.Data;
using Dapper;

namespace DammaniAPI.Features.Warranties;

// Monthly plan-limit state, shared by GetCreateWarrantyContext, CreateWarranty,
// and UpdateWarranty's draft activation (BP §13: 100% usage blocks NEW cards
// only; drafts never consume quota until activated).
public static class WarrantyUsage
{
    public class State
    {
        public int Used { get; set; }
        public int Limit { get; set; }
        public bool Blocked => Used >= Limit;
    }

    // BP §5 Free plan limit — permissive fallback if a shop somehow has no
    // subscription row (DMN-402 documented decision).
    private const int FallbackMonthlyLimit = 30;

    public static async Task<State> GetForShopAsync(IDbConnection db, IDbTransaction? tx, string shopId)
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

        // Cancelled cards still count: they consumed a card this month.
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

        return new State { Used = used, Limit = limit ?? FallbackMonthlyLimit };
    }
}
