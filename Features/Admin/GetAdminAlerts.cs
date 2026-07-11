using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetAdminAlerts
{
    public class Query : IRequest<Result> { }

    public class AlertItem
    {
        public string Id { get; set; } = "";
        public string Severity { get; set; } = "info"; // info | warning | danger
        public string Kind { get; set; } = "";
        public string TitleEn { get; set; } = "";
        public string TitleAr { get; set; } = "";
        public string? ShopId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<AlertItem> Items { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var items = new List<AlertItem>();

            var staleUpgrades = (await db.QueryAsync<(string ShopId, string ShopName, DateTime? UpdatedAt)>(
                """
                SELECT s.Id AS ShopId, s.Name AS ShopName, sub.UpdatedAt
                FROM Subscription sub
                JOIN Shop s ON s.Id = sub.ShopId
                JOIN Plan cur ON cur.Id = sub.PlanId
                JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                WHERE sched.SortOrder > cur.SortOrder
                  AND sub.UpdatedAt < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 24 HOUR)
                ORDER BY sub.UpdatedAt ASC
                LIMIT 10
                """)).ToList();

            foreach (var row in staleUpgrades)
            {
                items.Add(new AlertItem
                {
                    Id = $"upgrade-{row.ShopId}",
                    Severity = "warning",
                    Kind = "pending_upgrade",
                    TitleEn = $"Pending upgrade waiting >24h: {row.ShopName}",
                    TitleAr = $"ترقية معلّقة منذ أكثر من 24 ساعة: {row.ShopName}",
                    ShopId = row.ShopId,
                    CreatedAt = row.UpdatedAt
                });
            }

            var spikes = (await db.QueryAsync<(string ShopId, string ShopName, int Percent)>(
                """
                SELECT ShopId, ShopName, Percent FROM (
                    SELECT s.Id AS ShopId, s.Name AS ShopName,
                        FLOOR((SELECT COUNT(*) FROM Warranty w
                               WHERE w.ShopId = s.Id AND w.Status <> @Draft
                                 AND w.CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) * 100
                              / GREATEST(p.MonthlyCardLimit, 1)) AS Percent
                    FROM Shop s
                    JOIN Subscription sub ON sub.ShopId = s.Id
                    JOIN Plan p ON p.Id = sub.PlanId
                ) x
                WHERE Percent >= 90
                ORDER BY Percent DESC
                LIMIT 10
                """,
                new { Draft = WarrantyStatuses.Draft })).ToList();

            foreach (var row in spikes)
            {
                items.Add(new AlertItem
                {
                    Id = $"spike-{row.ShopId}",
                    Severity = "warning",
                    Kind = "usage_spike",
                    TitleEn = $"Usage spike {row.Percent}%: {row.ShopName}",
                    TitleAr = $"استخدام مرتفع {row.Percent}%: {row.ShopName}",
                    ShopId = row.ShopId
                });
            }

            var unread = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ContactMessage WHERE Status = @Unread",
                new { Unread = ContactMessageStatuses.Unread });
            if (unread > 0)
            {
                items.Add(new AlertItem
                {
                    Id = "inbox-unread",
                    Severity = "info",
                    Kind = "inbox",
                    TitleEn = $"{unread} unread contact message(s)",
                    TitleAr = $"{unread} رسالة تواصل غير مقروءة"
                });
            }

            var recentSuspensions = (await db.QueryAsync<(string ShopId, string ShopName, DateTime CreatedAt)>(
                """
                SELECT s.Id AS ShopId, s.Name AS ShopName, s.UpdatedAt AS CreatedAt
                FROM Shop s
                WHERE s.Status = 'suspended'
                  AND s.UpdatedAt >= DATE_SUB(UTC_TIMESTAMP(), INTERVAL 7 DAY)
                ORDER BY s.UpdatedAt DESC
                LIMIT 5
                """)).ToList();

            foreach (var row in recentSuspensions)
            {
                items.Add(new AlertItem
                {
                    Id = $"suspend-{row.ShopId}",
                    Severity = "danger",
                    Kind = "suspension",
                    TitleEn = $"Recently suspended: {row.ShopName}",
                    TitleAr = $"متجر موقوف حديثاً: {row.ShopName}",
                    ShopId = row.ShopId,
                    CreatedAt = row.CreatedAt
                });
            }

            return new Result { Items = items };
        }
    }
}
