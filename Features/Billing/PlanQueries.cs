using System.Data;
using Dapper;

namespace DammaniAPI.Features.Billing;

internal static class PlanQueries
{
    public static async Task<BillingPlan?> GetByCodeAsync(IDbConnection db, IDbTransaction? tx, string code)
        => await db.QueryFirstOrDefaultAsync<BillingPlan>(
            """
            SELECT Id, Code, NameEn, NameAr, PriceUsd, PriceIls, MonthlyCardLimit, MaxUsers,
                   HasBranches, HasExport, HasCustomTemplates, HasPrintableLabels,
                   ShowDamaaniBranding, HasAnalytics, SortOrder
            FROM Plan
            WHERE Code = @Code AND IsActive = 1
            """,
            new { Code = code },
            tx);

    public static async Task<BillingPlan?> GetByIdAsync(IDbConnection db, IDbTransaction? tx, string planId)
        => await db.QueryFirstOrDefaultAsync<BillingPlan>(
            """
            SELECT Id, Code, NameEn, NameAr, PriceUsd, PriceIls, MonthlyCardLimit, MaxUsers,
                   HasBranches, HasExport, HasCustomTemplates, HasPrintableLabels,
                   ShowDamaaniBranding, HasAnalytics, SortOrder
            FROM Plan
            WHERE Id = @Id
            """,
            new { Id = planId },
            tx);

    public static async Task<IReadOnlyList<BillingPlan>> ListActiveAsync(IDbConnection db, IDbTransaction? tx)
    {
        var rows = await db.QueryAsync<BillingPlan>(
            """
            SELECT Id, Code, NameEn, NameAr, PriceUsd, PriceIls, MonthlyCardLimit, MaxUsers,
                   HasBranches, HasExport, HasCustomTemplates, HasPrintableLabels,
                   ShowDamaaniBranding, HasAnalytics, SortOrder
            FROM Plan
            WHERE IsActive = 1
            ORDER BY SortOrder
            """,
            transaction: tx);
        return rows.AsList();
    }
}
