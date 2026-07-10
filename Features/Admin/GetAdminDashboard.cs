using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetAdminDashboard
{
    public class Query : IRequest<Result> { }

    public class Metrics
    {
        public int TotalShops { get; set; }
        public int PaidShops { get; set; }
        public int FreeShops { get; set; }
        public int WarrantiesThisMonth { get; set; }
        public int OpenServiceRequests { get; set; }
        public int PendingUpgradeRequests { get; set; }
    }

    public class UsageSpike
    {
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Percent { get; set; }
    }

    public class RecentShop
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? OwnerEmail { get; set; }
        public string PlanCode { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class PendingUpgrade
    {
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string RequestedPlanCode { get; set; } = "";
        public DateTime? RequestedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public Metrics Metrics { get; set; } = new();
        public List<UsageSpike> UsageSpikes { get; set; } = new();
        public List<RecentShop> RecentShops { get; set; } = new();
        public List<PendingUpgrade> PendingUpgrades { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();

            var metrics = await db.QueryFirstAsync<Metrics>(
                """
                SELECT
                    (SELECT COUNT(*) FROM Shop) AS TotalShops,
                    (SELECT COUNT(*)
                     FROM Subscription sub
                     JOIN Plan p ON p.Id = sub.PlanId
                     WHERE p.Code <> 'free') AS PaidShops,
                    (SELECT COUNT(*)
                     FROM Subscription sub
                     JOIN Plan p ON p.Id = sub.PlanId
                     WHERE p.Code = 'free') AS FreeShops,
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE Status <> @Draft
                       AND CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS WarrantiesThisMonth,
                    (SELECT COUNT(*)
                     FROM ServiceRequest
                     WHERE Status <> @Closed) AS OpenServiceRequests,
                    (SELECT COUNT(*)
                     FROM Subscription sub
                     JOIN Plan cur ON cur.Id = sub.PlanId
                     JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                     WHERE sched.SortOrder > cur.SortOrder) AS PendingUpgradeRequests
                """,
                new { Draft = WarrantyStatuses.Draft, Closed = ServiceRequestStatuses.Closed });

            var spikes = (await db.QueryAsync<UsageSpike>(
                """
                SELECT
                    s.Id AS ShopId,
                    s.Name AS ShopName,
                    shop_usage.Used,
                    shop_usage.`Limit`,
                    shop_usage.Percent
                FROM Shop s
                JOIN (
                    SELECT
                        sub.ShopId,
                        p.MonthlyCardLimit AS `Limit`,
                        (SELECT COUNT(*)
                         FROM Warranty w
                         WHERE w.ShopId = sub.ShopId
                           AND w.Status <> @Draft
                           AND w.CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS Used,
                        FLOOR(
                            (SELECT COUNT(*)
                             FROM Warranty w
                             WHERE w.ShopId = sub.ShopId
                               AND w.Status <> @Draft
                               AND w.CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) * 100
                            / GREATEST(p.MonthlyCardLimit, 1)) AS Percent
                    FROM Subscription sub
                    JOIN Plan p ON p.Id = sub.PlanId
                ) shop_usage ON shop_usage.ShopId = s.Id
                WHERE shop_usage.Percent >= 90
                ORDER BY shop_usage.Used DESC
                LIMIT 10
                """,
                new { Draft = WarrantyStatuses.Draft })).ToList();

            var recent = (await db.QueryAsync<RecentShop>(
                """
                SELECT
                    s.Id,
                    s.Name,
                    owner.Email AS OwnerEmail,
                    p.Code AS PlanCode,
                    s.Status,
                    s.CreatedAt
                FROM Shop s
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                LEFT JOIN ShopUser su ON su.ShopId = s.Id AND su.Role = @Owner AND su.Status = @Active
                LEFT JOIN User owner ON owner.Id = su.UserId
                ORDER BY s.CreatedAt DESC
                LIMIT 10
                """,
                new { Owner = Roles.Owner, Active = UserStatuses.Active })).ToList();

            var pending = (await db.QueryAsync<PendingUpgrade>(
                """
                SELECT
                    s.Id AS ShopId,
                    s.Name AS ShopName,
                    sched.Code AS RequestedPlanCode,
                    sub.UpdatedAt AS RequestedAt
                FROM Subscription sub
                JOIN Shop s ON s.Id = sub.ShopId
                JOIN Plan cur ON cur.Id = sub.PlanId
                JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                WHERE sched.SortOrder > cur.SortOrder
                ORDER BY sub.UpdatedAt DESC
                LIMIT 20
                """)).ToList();

            return new Result
            {
                Metrics = metrics,
                UsageSpikes = spikes,
                RecentShops = recent,
                PendingUpgrades = pending
            };
        }
    }
}
