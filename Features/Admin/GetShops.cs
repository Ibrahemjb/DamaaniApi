using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetShops
{
    public class Query : IRequest<Result>
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? PlanCode { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class ShopRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? OwnerEmail { get; set; }
        public string Country { get; set; } = "";
        public string PlanCode { get; set; } = "";
        public string PlanNameEn { get; set; } = "";
        public string PlanNameAr { get; set; } = "";
        public int Used { get; set; }
        public int Limit { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<ShopRow> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var offset = (page - 1) * pageSize;
            var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
            var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToLowerInvariant();
            var planCode = string.IsNullOrWhiteSpace(request.PlanCode) ? null : request.PlanCode.Trim().ToLowerInvariant();

            using var db = _mdb.Open();
            var total = await db.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM Shop s
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                LEFT JOIN ShopUser su ON su.ShopId = s.Id AND su.Role = 'owner' AND su.Status = 'active'
                LEFT JOIN User owner ON owner.Id = su.UserId
                WHERE (@Status IS NULL OR s.Status = @Status)
                  AND (@PlanCode IS NULL OR p.Code = @PlanCode)
                  AND (@Search IS NULL OR s.Name LIKE @Search OR owner.Email LIKE @Search)
                """,
                new { Search = search, Status = status, PlanCode = planCode });

            var items = (await db.QueryAsync<ShopRow>(
                """
                SELECT
                    s.Id,
                    s.Name,
                    owner.Email AS OwnerEmail,
                    s.Country,
                    COALESCE(p.Code, 'free') AS PlanCode,
                    COALESCE(p.NameEn, 'Free') AS PlanNameEn,
                    COALESCE(p.NameAr, 'المجانية') AS PlanNameAr,
                    (SELECT COUNT(*)
                     FROM Warranty w
                     WHERE w.ShopId = s.Id
                       AND w.Status <> 'draft'
                       AND w.CreatedAt >= DATE_FORMAT(UTC_DATE(), '%Y-%m-01')) AS Used,
                    COALESCE(p.MonthlyCardLimit, 30) AS `Limit`,
                    s.Status,
                    s.CreatedAt
                FROM Shop s
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                LEFT JOIN ShopUser su ON su.ShopId = s.Id AND su.Role = 'owner' AND su.Status = 'active'
                LEFT JOIN User owner ON owner.Id = su.UserId
                WHERE (@Status IS NULL OR s.Status = @Status)
                  AND (@PlanCode IS NULL OR p.Code = @PlanCode)
                  AND (@Search IS NULL OR s.Name LIKE @Search OR owner.Email LIKE @Search)
                ORDER BY s.CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                new { Search = search, Status = status, PlanCode = planCode, PageSize = pageSize, Offset = offset })).ToList();

            return new Result { Items = items, Total = total, Page = page, PageSize = pageSize };
        }
    }
}
