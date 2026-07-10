using System.Data;
using DammaniAPI.Features.Billing;

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

    public static async Task<State> GetForShopAsync(IDbConnection db, IDbTransaction? tx, string shopId)
    {
        var usage = await UsageService.GetUsageAsync(db, tx, shopId);
        return new State { Used = usage.Used, Limit = usage.Limit };
    }
}
