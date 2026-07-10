using DammaniAPI.Features.Billing;

namespace DammaniAPI.Features.Admin;

internal static class AdminPlanRules
{
    internal static bool IsUpgradeRequest(BillingPlan? current, BillingPlan? scheduled)
        => current is not null && scheduled is not null && scheduled.SortOrder > current.SortOrder;

    internal static bool IsDowngradeSchedule(BillingPlan? current, BillingPlan? scheduled)
        => current is not null && scheduled is not null && scheduled.SortOrder < current.SortOrder;
}
