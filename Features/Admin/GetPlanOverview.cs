using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Billing;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetPlanOverview
{
    public class Query : IRequest<Result> { }

    public class PlanCount
    {
        public string Code { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string NameAr { get; set; } = "";
        public int Count { get; set; }
    }

    public class PendingUpgrade
    {
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string CurrentPlanCode { get; set; } = "";
        public string RequestedPlanCode { get; set; } = "";
        public decimal AmountUsd { get; set; }
        public decimal AmountIls { get; set; }
        public string? ReferenceCode { get; set; }
        public DateTime? RequestedAt { get; set; }
    }

    public class EndingShop
    {
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string PlanCode { get; set; } = "";
        public DateTime CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<PlanCount> PlanCounts { get; set; } = new();
        public List<PendingUpgrade> PendingUpgrades { get; set; } = new();
        public List<EndingShop> EndingSoon { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();

            var planCounts = (await db.QueryAsync<PlanCount>(
                """
                SELECT p.Code, p.NameEn, p.NameAr, COUNT(sub.Id) AS Count
                FROM Plan p
                LEFT JOIN Subscription sub ON sub.PlanId = p.Id
                WHERE p.IsActive = 1
                GROUP BY p.Id, p.Code, p.NameEn, p.NameAr, p.SortOrder
                ORDER BY p.SortOrder
                """)).ToList();

            var pendingRows = (await db.QueryAsync<(string ShopId, string ShopName, string CurrentPlanCode, string RequestedPlanCode, decimal AmountUsd, decimal AmountIls, DateTime? RequestedAt, string? Details)>(
                """
                SELECT
                    s.Id AS ShopId,
                    s.Name AS ShopName,
                    cur.Code AS CurrentPlanCode,
                    sched.Code AS RequestedPlanCode,
                    sched.PriceUsd AS AmountUsd,
                    sched.PriceIls AS AmountIls,
                    sub.UpdatedAt AS RequestedAt,
                    (SELECT a.Details
                     FROM ActivityLog a
                     WHERE a.ShopId = s.Id AND a.Action = 'subscription.upgrade_requested'
                     ORDER BY a.CreatedAt DESC LIMIT 1) AS Details
                FROM Subscription sub
                JOIN Shop s ON s.Id = sub.ShopId
                JOIN Plan cur ON cur.Id = sub.PlanId
                JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                WHERE sched.SortOrder > cur.SortOrder
                ORDER BY sub.UpdatedAt DESC
                """)).ToList();

            var pending = pendingRows.Select(row => new PendingUpgrade
            {
                ShopId = row.ShopId,
                ShopName = row.ShopName,
                CurrentPlanCode = row.CurrentPlanCode,
                RequestedPlanCode = row.RequestedPlanCode,
                AmountUsd = row.AmountUsd,
                AmountIls = row.AmountIls,
                RequestedAt = row.RequestedAt,
                ReferenceCode = ExtractReference(row.Details)
            }).ToList();

            var ending = (await db.QueryAsync<EndingShop>(
                """
                SELECT
                    s.Id AS ShopId,
                    s.Name AS ShopName,
                    p.Code AS PlanCode,
                    sub.CurrentPeriodEnd,
                    sub.CancelAtPeriodEnd
                FROM Subscription sub
                JOIN Shop s ON s.Id = sub.ShopId
                JOIN Plan p ON p.Id = sub.PlanId
                WHERE sub.CancelAtPeriodEnd = 1
                   OR sub.CurrentPeriodEnd <= DATE_ADD(UTC_DATE(), INTERVAL 14 DAY)
                ORDER BY sub.CurrentPeriodEnd
                LIMIT 50
                """)).ToList();

            return new Result { PlanCounts = planCounts, PendingUpgrades = pending, EndingSoon = ending };
        }

        internal static string? ExtractReference(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
                return null;
            const string marker = "\"reference\":\"";
            var start = detailsJson.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;
            start += marker.Length;
            var end = detailsJson.IndexOf('"', start);
            return end < 0 ? null : detailsJson[start..end];
        }
    }
}
