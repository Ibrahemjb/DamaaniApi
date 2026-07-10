using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Features.Warranties;
using MediatR;

namespace DammaniAPI.Features.Dashboard;

public class GetSummary
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class Metrics
    {
        public int WarrantiesThisMonth { get; set; }
        public int ActiveWarranties { get; set; }
        public int ExpiringSoon { get; set; }
        public int OpenRequests { get; set; }
    }

    public class UsageSummary
    {
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Percent { get; set; }
        public string Level { get; set; } = "normal";
    }

    public class RecentWarranty
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductName { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RecentRequest
    {
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Checklist
    {
        public bool HasLogo { get; set; }
        public bool HasTemplate { get; set; }
        public bool HasWarranty { get; set; }
        public bool HasShared { get; set; }
        public bool OnboardingCompleted { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? ShopName { get; set; }
        public Metrics Metrics { get; set; } = new();
        public UsageSummary Usage { get; set; } = new();
        public List<RecentWarranty> RecentWarranties { get; set; } = new();
        public List<RecentRequest> RecentRequests { get; set; } = new();
        public Checklist Checklist { get; set; } = new();
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

            var shopRow = await db.QueryFirstOrDefaultAsync<ShopChecklistRow>(
                """
                SELECT
                    s.Name AS ShopName,
                    (s.LogoPath IS NOT NULL AND s.LogoPath <> '') AS HasLogo,
                    s.OnboardingCompletedAt IS NOT NULL AS OnboardingCompleted,
                    EXISTS(SELECT 1 FROM WarrantyTemplate t WHERE t.ShopId = s.Id) AS HasTemplate,
                    EXISTS(SELECT 1 FROM Warranty w WHERE w.ShopId = s.Id AND w.Status <> @Draft) AS HasWarranty,
                    EXISTS(SELECT 1 FROM ActivityLog a WHERE a.ShopId = s.Id AND a.Action = 'warranty.shared') AS HasShared
                FROM Shop s
                WHERE s.Id = @ShopId
                """,
                new { request.ShopId, Draft = WarrantyStatuses.Draft });

            if (shopRow == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var metrics = await db.QueryFirstAsync<Metrics>(
                """
                SELECT
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE ShopId = @ShopId
                       AND Status <> @Draft
                       AND CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS WarrantiesThisMonth,
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE ShopId = @ShopId
                       AND Status = @Active
                       AND ExpiryDate >= CURDATE()) AS ActiveWarranties,
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE ShopId = @ShopId
                       AND Status = @Active
                       AND ExpiryDate >= CURDATE()
                       AND ExpiryDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)) AS ExpiringSoon,
                    (SELECT COUNT(*)
                     FROM ServiceRequest
                     WHERE ShopId = @ShopId
                       AND Status <> @Closed) AS OpenRequests
                """,
                new
                {
                    request.ShopId,
                    Draft = WarrantyStatuses.Draft,
                    Active = WarrantyStatuses.Active,
                    Closed = ServiceRequestStatuses.Closed
                });

            var usage = await UsageService.GetUsageAsync(db, null, request.ShopId);

            var recentWarranties = (await db.QueryAsync<RecentWarranty>(
                $"""
                SELECT
                    w.Id,
                    w.Code,
                    c.Name AS CustomerName,
                    w.ProductName,
                    {WarrantyListFilter.DerivedStatusSql} AS Status,
                    w.CreatedAt
                FROM Warranty w
                JOIN Customer c ON c.Id = w.CustomerId
                WHERE w.ShopId = @ShopId
                ORDER BY w.CreatedAt DESC
                LIMIT 5
                """,
                new { request.ShopId })).ToList();

            var recentRequests = (await db.QueryAsync<RecentRequest>(
                """
                SELECT Id, RequestNumber, CustomerName, Status, CreatedAt
                FROM ServiceRequest
                WHERE ShopId = @ShopId
                ORDER BY CreatedAt DESC
                LIMIT 5
                """,
                new { request.ShopId })).ToList();

            return new Result
            {
                Success = true,
                ShopName = shopRow.ShopName,
                Metrics = metrics,
                Usage = new UsageSummary
                {
                    Used = usage.Used,
                    Limit = usage.Limit,
                    Percent = usage.Percent,
                    Level = usage.Level
                },
                RecentWarranties = recentWarranties,
                RecentRequests = recentRequests,
                Checklist = new Checklist
                {
                    HasLogo = shopRow.HasLogo,
                    HasTemplate = shopRow.HasTemplate,
                    HasWarranty = shopRow.HasWarranty,
                    HasShared = shopRow.HasShared,
                    OnboardingCompleted = shopRow.OnboardingCompleted
                }
            };
        }

        private sealed class ShopChecklistRow
        {
            public string ShopName { get; set; } = "";
            public bool HasLogo { get; set; }
            public bool HasTemplate { get; set; }
            public bool HasWarranty { get; set; }
            public bool HasShared { get; set; }
            public bool OnboardingCompleted { get; set; }
        }
    }

    // ponytail: mirrors SQL metric predicates for unit tests without MySQL.
    internal static class MetricRules
    {
        internal static bool CountsAsWarrantyThisMonth(DateTime createdAtUtc, string status, DateTime utcNow)
        {
            if (status == WarrantyStatuses.Draft)
                return false;
            var (monthStart, _) = UsageService.GetUtcMonthPeriod(utcNow);
            return createdAtUtc >= monthStart;
        }

        internal static bool IsActiveWarranty(string status, DateTime? expiryDate, DateTime today)
            => status == WarrantyStatuses.Active
               && expiryDate.HasValue
               && expiryDate.Value.Date >= today.Date;

        internal static bool IsExpiringSoon(string status, DateTime? expiryDate, DateTime today)
            => IsActiveWarranty(status, expiryDate, today)
               && expiryDate!.Value.Date <= today.Date.AddDays(30);

        internal static bool IsOpenRequest(string status)
            => !string.Equals(status, ServiceRequestStatuses.Closed, StringComparison.OrdinalIgnoreCase);
    }
}
