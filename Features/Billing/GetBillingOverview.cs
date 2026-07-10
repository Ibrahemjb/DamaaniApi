using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class GetBillingOverview
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public BillingPlan? CurrentPlan { get; set; }
        public BillingPlan? ScheduledPlan { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public string? CancelReason { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
        public string PendingChangeType { get; set; } = "none"; // none | upgrade | downgrade | cancel
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Percent { get; set; }
        public string Level { get; set; } = "normal";
        public bool Blocked { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            await SubscriptionRoller.RollIfNeededAsync(db, null, request.ShopId, DateTime.UtcNow);

            var sub = await SubscriptionRoller.LoadAsync(db, null, request.ShopId);
            if (sub is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var currentPlan = await PlanQueries.GetByIdAsync(db, null, sub.PlanId);
            BillingPlan? scheduledPlan = null;
            if (!string.IsNullOrWhiteSpace(sub.ScheduledPlanId))
                scheduledPlan = await PlanQueries.GetByIdAsync(db, null, sub.ScheduledPlanId);

            var usage = await UsageService.GetUsageAsync(db, null, request.ShopId);
            var pendingType = ResolvePendingChange(sub.CancelAtPeriodEnd, currentPlan, scheduledPlan);

            return new Result
            {
                Success = true,
                CurrentPlan = currentPlan,
                ScheduledPlan = scheduledPlan,
                CancelAtPeriodEnd = sub.CancelAtPeriodEnd,
                CancelReason = sub.CancelReason,
                CurrentPeriodStart = sub.CurrentPeriodStart,
                CurrentPeriodEnd = sub.CurrentPeriodEnd,
                PendingChangeType = pendingType,
                Used = usage.Used,
                Limit = usage.Limit,
                Percent = usage.Percent,
                Level = usage.Level,
                Blocked = usage.Blocked
            };
        }

        internal static string ResolvePendingChange(bool cancelAtPeriodEnd, BillingPlan? current, BillingPlan? scheduled)
        {
            if (cancelAtPeriodEnd)
                return "cancel";
            if (scheduled is null || current is null)
                return "none";
            if (scheduled.SortOrder > current.SortOrder)
                return "upgrade";
            if (scheduled.SortOrder < current.SortOrder)
                return "downgrade";
            return "none";
        }
    }
}
