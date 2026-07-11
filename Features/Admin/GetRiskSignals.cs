using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetRiskSignals
{
    public class Query : IRequest<Result> { }

    public class RiskItem
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Severity { get; set; } = "warning";
        public string TitleEn { get; set; } = "";
        public string TitleAr { get; set; } = "";
        public string? ShopId { get; set; }
        public string? ShopName { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<RiskItem> Items { get; set; } = new();
        public List<ShopSummary> SuspendedShops { get; set; } = new();
        public List<SpikeSummary> UsageSpikes { get; set; } = new();
    }

    public class ShopSummary
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? OwnerEmail { get; set; }
        public string? SuspensionNote { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class SpikeSummary
    {
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Percent { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var items = new List<RiskItem>();

            // Free plan near/at limit with many cards created in last 24h
            var velocity = (await db.QueryAsync<(string ShopId, string ShopName, int Used24h, int Limit)>(
                """
                SELECT ShopId, ShopName, Used24h, `Limit` FROM (
                    SELECT s.Id AS ShopId, s.Name AS ShopName,
                        (SELECT COUNT(*) FROM Warranty w
                         WHERE w.ShopId = s.Id AND w.Status <> @Draft
                           AND w.CreatedAt >= DATE_SUB(UTC_TIMESTAMP(), INTERVAL 24 HOUR)) AS Used24h,
                        p.MonthlyCardLimit AS `Limit`
                    FROM Shop s
                    JOIN Subscription sub ON sub.ShopId = s.Id
                    JOIN Plan p ON p.Id = sub.PlanId
                    WHERE p.Code = 'free'
                      AND s.Status = 'active'
                ) x
                WHERE Used24h >= GREATEST(FLOOR(`Limit` * 0.8), 10)
                """,
                new { Draft = WarrantyStatuses.Draft })).ToList();

            foreach (var row in velocity)
            {
                items.Add(new RiskItem
                {
                    Id = $"velocity-{row.ShopId}",
                    Kind = "card_velocity",
                    Severity = "danger",
                    TitleEn = $"Free plan velocity: {row.ShopName} created {row.Used24h} cards in 24h (limit {row.Limit})",
                    TitleAr = $"سرعة إنشاء عالية: {row.ShopName} أنشأ {row.Used24h} بطاقة خلال 24 ساعة",
                    ShopId = row.ShopId,
                    ShopName = row.ShopName
                });
            }

            var dupEmails = (await db.QueryAsync<(string Email, int ShopCount)>(
                """
                SELECT LOWER(u.Email) AS Email, COUNT(DISTINCT su.ShopId) AS ShopCount
                FROM User u
                JOIN ShopUser su ON su.UserId = u.Id AND su.Role = 'owner'
                WHERE u.IsPlatformAdmin = 0
                GROUP BY LOWER(u.Email)
                HAVING ShopCount > 1
                """)).ToList();

            foreach (var row in dupEmails)
            {
                items.Add(new RiskItem
                {
                    Id = $"dup-email-{row.Email}",
                    Kind = "duplicate_owner_email",
                    Severity = "warning",
                    TitleEn = $"Owner email on {row.ShopCount} shops: {row.Email}",
                    TitleAr = $"بريد المالك مرتبط بـ {row.ShopCount} متاجر: {row.Email}"
                });
            }

            var emptyNames = (await db.QueryAsync<(string Id, string Name)>(
                """
                SELECT Id, Name FROM Shop
                WHERE TRIM(Name) = '' OR Name REGEXP '^[0-9]+$' OR LENGTH(TRIM(Name)) < 2
                LIMIT 20
                """)).ToList();

            foreach (var row in emptyNames)
            {
                items.Add(new RiskItem
                {
                    Id = $"name-{row.Id}",
                    Kind = "suspicious_name",
                    Severity = "warning",
                    TitleEn = $"Suspicious shop name: '{row.Name}'",
                    TitleAr = $"اسم متجر مشبوه: '{row.Name}'",
                    ShopId = row.Id,
                    ShopName = row.Name
                });
            }

            var suspended = (await db.QueryAsync<ShopSummary>(
                """
                SELECT s.Id, s.Name, owner.Email AS OwnerEmail, s.SuspensionNote, s.UpdatedAt
                FROM Shop s
                LEFT JOIN ShopUser su ON su.ShopId = s.Id AND su.Role = 'owner' AND su.Status = 'active'
                LEFT JOIN User owner ON owner.Id = su.UserId
                WHERE s.Status = 'suspended'
                ORDER BY s.UpdatedAt DESC
                LIMIT 50
                """)).ToList();

            var spikes = (await db.QueryAsync<SpikeSummary>(
                """
                SELECT ShopId, ShopName, Used, `Limit`, Percent FROM (
                    SELECT s.Id AS ShopId, s.Name AS ShopName,
                        (SELECT COUNT(*) FROM Warranty w
                         WHERE w.ShopId = s.Id AND w.Status <> @Draft
                           AND w.CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS Used,
                        p.MonthlyCardLimit AS `Limit`,
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
                LIMIT 20
                """,
                new { Draft = WarrantyStatuses.Draft })).ToList();

            return new Result { Items = items, SuspendedShops = suspended, UsageSpikes = spikes };
        }
    }
}
