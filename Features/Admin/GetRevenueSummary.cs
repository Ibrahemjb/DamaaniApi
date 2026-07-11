using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetRevenueSummary
{
    public class Query : IRequest<Result> { }

    public class Funnel
    {
        public int Signups { get; set; }
        public int Onboarded { get; set; }
        public int FirstWarranty { get; set; }
        public int Paid { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public decimal MrrUsd { get; set; }
        public decimal MrrIls { get; set; }
        public int PaidShops { get; set; }
        public int FreeShops { get; set; }
        public int NewPaidThisMonth { get; set; }
        public int ChurningShops { get; set; }
        public decimal PendingUpgradePipelineUsd { get; set; }
        public decimal PendingUpgradePipelineIls { get; set; }
        public int WarrantiesThisMonth { get; set; }
        public int WarrantiesLastMonth { get; set; }
        public Funnel ActivationFunnel { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var row = await db.QueryFirstAsync<Result>(
                """
                SELECT
                    COALESCE((
                        SELECT SUM(p.PriceUsd)
                        FROM Subscription sub
                        JOIN Plan p ON p.Id = sub.PlanId
                        WHERE p.Code <> 'free' AND sub.Status = 'active'
                    ), 0) AS MrrUsd,
                    COALESCE((
                        SELECT SUM(p.PriceIls)
                        FROM Subscription sub
                        JOIN Plan p ON p.Id = sub.PlanId
                        WHERE p.Code <> 'free' AND sub.Status = 'active'
                    ), 0) AS MrrIls,
                    (SELECT COUNT(*) FROM Subscription sub JOIN Plan p ON p.Id = sub.PlanId WHERE p.Code <> 'free') AS PaidShops,
                    (SELECT COUNT(*) FROM Subscription sub JOIN Plan p ON p.Id = sub.PlanId WHERE p.Code = 'free') AS FreeShops,
                    (SELECT COUNT(DISTINCT pay.ShopId)
                     FROM Payment pay
                     JOIN Plan p ON p.Id = pay.PlanId
                     WHERE pay.Status = 'paid' AND p.Code <> 'free'
                       AND pay.PaidAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS NewPaidThisMonth,
                    (SELECT COUNT(*) FROM Subscription WHERE CancelAtPeriodEnd = 1) AS ChurningShops,
                    COALESCE((
                        SELECT SUM(sched.PriceUsd)
                        FROM Subscription sub
                        JOIN Plan cur ON cur.Id = sub.PlanId
                        JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                        WHERE sched.SortOrder > cur.SortOrder
                    ), 0) AS PendingUpgradePipelineUsd,
                    COALESCE((
                        SELECT SUM(sched.PriceIls)
                        FROM Subscription sub
                        JOIN Plan cur ON cur.Id = sub.PlanId
                        JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                        WHERE sched.SortOrder > cur.SortOrder
                    ), 0) AS PendingUpgradePipelineIls,
                    (SELECT COUNT(*) FROM Warranty
                     WHERE Status <> @Draft AND CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS WarrantiesThisMonth,
                    (SELECT COUNT(*) FROM Warranty
                     WHERE Status <> @Draft
                       AND CreatedAt >= DATE_FORMAT(DATE_SUB(UTC_DATE(), INTERVAL 1 MONTH), '%Y-%m-01')
                       AND CreatedAt < DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS WarrantiesLastMonth
                """,
                new { Draft = WarrantyStatuses.Draft });

            row.ActivationFunnel = await db.QueryFirstAsync<Funnel>(
                """
                SELECT
                    (SELECT COUNT(*) FROM User WHERE IsPlatformAdmin = 0) AS Signups,
                    (SELECT COUNT(*) FROM Shop WHERE OnboardingCompletedAt IS NOT NULL) AS Onboarded,
                    (SELECT COUNT(DISTINCT ShopId) FROM Warranty WHERE Status <> @Draft) AS FirstWarranty,
                    (SELECT COUNT(*) FROM Subscription sub JOIN Plan p ON p.Id = sub.PlanId WHERE p.Code <> 'free') AS Paid
                """,
                new { Draft = WarrantyStatuses.Draft });

            row.Success = true;
            return row;
        }
    }
}
