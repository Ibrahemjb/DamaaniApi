using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Features.Warranties;
using MediatR;

namespace DammaniAPI.Features.Reports;

public class GetReports
{
    public const int MaxRangeDays = 366;

    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class Cards
    {
        public int WarrantiesInRange { get; set; }
        public int ActiveWarranties { get; set; }
        public int ExpiredWarranties { get; set; }
        public int OpenRequests { get; set; }
    }

    public class CategoryCount
    {
        public string? Category { get; set; }
        public int Count { get; set; }
    }

    public class StatusCount
    {
        public string? Status { get; set; }
        public int Count { get; set; }
    }

    public class ExpiringWarranty
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductName { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class ProblemTypeCount
    {
        public string? ProblemType { get; set; }
        public int Count { get; set; }
    }

    public class RepeatProduct
    {
        public string? ProductName { get; set; }
        public int RequestCount { get; set; }
        public int WarrantyCount { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public Cards Cards { get; set; } = new();
        public List<CategoryCount> WarrantiesByCategory { get; set; } = new();
        public List<StatusCount> RequestsByStatus { get; set; } = new();
        public List<ExpiringWarranty> ExpiringSoon { get; set; } = new();
        public List<ProblemTypeCount> CommonProblemTypes { get; set; } = new();
        public List<RepeatProduct> RepeatServiceProducts { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var (from, to, rangeError) = ResolveRange(request.From, request.To, DateTime.UtcNow);
            if (rangeError != null)
                return new Result { Success = false, ErrorCode = rangeError };

            using var db = _mdb.Open();

            var cards = await db.QueryFirstAsync<Cards>(
                """
                SELECT
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE ShopId = @ShopId
                       AND Status <> @Draft
                       AND CreatedAt >= @From
                       AND CreatedAt < @To) AS WarrantiesInRange,
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE ShopId = @ShopId
                       AND Status = @Active
                       AND ExpiryDate IS NOT NULL
                       AND ExpiryDate >= CURDATE()) AS ActiveWarranties,
                    (SELECT COUNT(*)
                     FROM Warranty
                     WHERE ShopId = @ShopId
                       AND Status = @Active
                       AND ExpiryDate IS NOT NULL
                       AND ExpiryDate < CURDATE()) AS ExpiredWarranties,
                    (SELECT COUNT(*)
                     FROM ServiceRequest
                     WHERE ShopId = @ShopId
                       AND Status <> @Closed) AS OpenRequests
                """,
                new
                {
                    request.ShopId,
                    from,
                    to,
                    Draft = WarrantyStatuses.Draft,
                    Active = WarrantyStatuses.Active,
                    Closed = ServiceRequestStatuses.Closed
                });

            var warrantiesByCategory = (await db.QueryAsync<CategoryCount>(
                """
                SELECT COALESCE(NULLIF(Category, ''), 'other') AS Category, COUNT(*) AS Count
                FROM Warranty
                WHERE ShopId = @ShopId
                  AND Status <> @Draft
                  AND CreatedAt >= @From
                  AND CreatedAt < @To
                GROUP BY COALESCE(NULLIF(Category, ''), 'other')
                ORDER BY Count DESC, Category
                """,
                new { request.ShopId, from, to, Draft = WarrantyStatuses.Draft })).ToList();

            var requestsByStatus = (await db.QueryAsync<StatusCount>(
                """
                SELECT Status, COUNT(*) AS Count
                FROM ServiceRequest
                WHERE ShopId = @ShopId
                  AND CreatedAt >= @From
                  AND CreatedAt < @To
                GROUP BY Status
                ORDER BY Count DESC, Status
                """,
                new { request.ShopId, from, to })).ToList();

            var expiringSoon = (await db.QueryAsync<ExpiringWarranty>(
                """
                SELECT w.Id, w.Code, c.Name AS CustomerName, w.ProductName, w.ExpiryDate
                FROM Warranty w
                JOIN Customer c ON c.Id = w.CustomerId
                WHERE w.ShopId = @ShopId
                  AND w.Status = @Active
                  AND w.ExpiryDate IS NOT NULL
                  AND w.ExpiryDate >= CURDATE()
                  AND w.ExpiryDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)
                ORDER BY w.ExpiryDate ASC, w.Code
                LIMIT 20
                """,
                new { request.ShopId, Active = WarrantyStatuses.Active })).ToList();

            var commonProblemTypes = (await db.QueryAsync<ProblemTypeCount>(
                """
                SELECT ProblemType, COUNT(*) AS Count
                FROM ServiceRequest
                WHERE ShopId = @ShopId
                  AND CreatedAt >= @From
                  AND CreatedAt < @To
                GROUP BY ProblemType
                ORDER BY Count DESC, ProblemType
                """,
                new { request.ShopId, from, to })).ToList();

            var repeatServiceProducts = (await db.QueryAsync<RepeatProduct>(
                """
                SELECT
                    w.ProductName,
                    COUNT(*) AS RequestCount,
                    COUNT(DISTINCT sr.WarrantyId) AS WarrantyCount
                FROM ServiceRequest sr
                JOIN Warranty w ON w.Id = sr.WarrantyId
                WHERE sr.ShopId = @ShopId
                  AND sr.CreatedAt >= @From
                  AND sr.CreatedAt < @To
                GROUP BY w.ProductName
                HAVING COUNT(*) >= 2
                ORDER BY RequestCount DESC, w.ProductName
                LIMIT 10
                """,
                new { request.ShopId, from, to })).ToList();

            return new Result
            {
                Success = true,
                From = from,
                To = to,
                Cards = cards,
                WarrantiesByCategory = warrantiesByCategory,
                RequestsByStatus = requestsByStatus,
                ExpiringSoon = expiringSoon,
                CommonProblemTypes = commonProblemTypes,
                RepeatServiceProducts = repeatServiceProducts
            };
        }
    }

    internal static (DateTime From, DateTime To, string? ErrorCode) ResolveRange(
        DateTime? fromInput,
        DateTime? toInput,
        DateTime utcNow)
    {
        DateTime from;
        DateTime to;

        if (fromInput.HasValue || toInput.HasValue)
        {
            if (!fromInput.HasValue || !toInput.HasValue)
                return (default, default, ErrorCodes.InvalidRange);

            from = ToUtcStart(fromInput.Value);
            to = ToUtcStart(toInput.Value);

            if (from > to)
                return (default, default, ErrorCodes.InvalidRange);

            if ((to - from).TotalDays > MaxRangeDays)
                return (default, default, ErrorCodes.InvalidRange);
        }
        else
        {
            (from, to) = UsageService.GetUtcMonthPeriod(utcNow);
        }

        return (from, to, null);
    }

    internal static DateTime ToUtcStart(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    // ponytail: mirrors SQL predicates for unit tests without MySQL.
    internal static class MetricRules
    {
        internal static bool CountsAsWarrantyInRange(
            DateTime createdAtUtc,
            string status,
            DateTime from,
            DateTime to)
            => status != WarrantyStatuses.Draft
               && createdAtUtc >= from
               && createdAtUtc < to;

        internal static bool IsExpiredWarranty(string status, DateTime? expiryDate, DateTime today)
            => status == WarrantyStatuses.Active
               && expiryDate.HasValue
               && expiryDate.Value.Date < today.Date;

        internal static bool IsActiveWarranty(string status, DateTime? expiryDate, DateTime today)
            => status == WarrantyStatuses.Active
               && expiryDate.HasValue
               && expiryDate.Value.Date >= today.Date;

        internal static bool IsExpiringSoon(string status, DateTime? expiryDate, DateTime today)
            => IsActiveWarranty(status, expiryDate, today)
               && expiryDate!.Value.Date <= today.Date.AddDays(30);

        internal static bool IsOpenRequest(string status)
            => !string.Equals(status, ServiceRequestStatuses.Closed, StringComparison.OrdinalIgnoreCase);

        internal static bool CountsAsRequestInRange(DateTime createdAtUtc, DateTime from, DateTime to)
            => createdAtUtc >= from && createdAtUtc < to;
    }
}
