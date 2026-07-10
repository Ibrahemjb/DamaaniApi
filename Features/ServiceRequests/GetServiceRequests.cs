using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Shop service-request queue with search, filters, pagination, and unfiltered
// per-status counts for filter chips (DMN-601, BP §10.18).
public class GetServiceRequests
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Category { get; set; }
        public string? WarrantyStatus { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? BranchId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ServiceRequestListItem
    {
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? ProductName { get; set; }
        public string? WarrantyCode { get; set; }
        public string? ProblemType { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AssignedToName { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<ServiceRequestListItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        // Unfiltered shop totals — chips always show full-queue counts (DMN-601).
        public Dictionary<string, int> StatusCounts { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var (whereSql, parameters) = ServiceRequestListFilter.Build(request.ShopId, new ServiceRequestListFilter.Args
            {
                Search = request.Search,
                Status = request.Status,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                Category = request.Category,
                WarrantyStatus = request.WarrantyStatus,
                AssignedToUserId = request.AssignedToUserId,
                BranchId = request.BranchId
            });

            using var db = _mdb.Open();

            var statusCounts = ServiceRequestStatuses.Supported.ToDictionary(s => s, _ => 0);
            var countRows = await db.QueryAsync<(string Status, int Count)>(
                "SELECT Status, COUNT(*) AS Count FROM ServiceRequest WHERE ShopId = @ShopId GROUP BY Status",
                new { request.ShopId });
            foreach (var row in countRows)
            {
                if (statusCounts.ContainsKey(row.Status))
                    statusCounts[row.Status] = row.Count;
            }

            var fromSql = """
                FROM ServiceRequest sr
                LEFT JOIN Warranty w ON w.Id = sr.WarrantyId
                LEFT JOIN User u ON u.Id = sr.AssignedToUserId
                """;

            var totalCount = await db.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) {fromSql} WHERE {whereSql}",
                parameters);

            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);
            var items = (await db.QueryAsync<ServiceRequestListItem>(
                $"""
                SELECT sr.Id, sr.RequestNumber, sr.CustomerName, sr.CustomerPhone,
                       COALESCE(w.ProductName, '') AS ProductName,
                       w.Code AS WarrantyCode, sr.ProblemType, sr.Status, sr.CreatedAt,
                       u.FullName AS AssignedToName
                {fromSql}
                WHERE {whereSql}
                ORDER BY sr.CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                parameters)).ToList();

            return new Result
            {
                Success = true,
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                StatusCounts = statusCounts
            };
        }
    }
}
