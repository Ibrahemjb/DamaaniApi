using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Customers;

// Operational customer lookup by name or phone with aggregate counts (DMN-701).
public class SearchCustomers
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? Term { get; set; }
        public int Limit { get; set; } = 20;
    }

    public class CustomerSearchItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public int WarrantyCount { get; set; }
        public int OpenRequestCount { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<CustomerSearchItem> Items { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var term = request.Term?.Trim() ?? "";
            if (term.Length < 2)
                return new Result { Success = true, Items = new() };

            var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 20);
            var (whereSql, parameters) = CustomerSearchFilter.Build(request.ShopId, term);
            parameters.Add("Limit", limit);

            using var db = _mdb.Open();
            var items = (await db.QueryAsync<CustomerSearchItem>(
                $"""
                SELECT c.Id, c.Name, c.Phone,
                       (SELECT COUNT(*) FROM Warranty w
                        WHERE w.CustomerId = c.Id AND w.ShopId = @ShopId
                          AND w.Status <> 'draft') AS WarrantyCount,
                       (SELECT COUNT(*) FROM ServiceRequest sr
                        WHERE sr.CustomerPhone = c.Phone AND sr.ShopId = @ShopId
                          AND sr.Status <> 'closed') AS OpenRequestCount,
                       (
                         SELECT MAX(ts) FROM (
                           SELECT w.CreatedAt AS ts FROM Warranty w
                           WHERE w.CustomerId = c.Id AND w.ShopId = @ShopId
                           UNION ALL
                           SELECT sr.CreatedAt AS ts FROM ServiceRequest sr
                           WHERE sr.CustomerPhone = c.Phone AND sr.ShopId = @ShopId
                         ) activity
                       ) AS LastActivityAt
                FROM Customer c
                WHERE {whereSql}
                ORDER BY LastActivityAt DESC
                LIMIT @Limit
                """,
                parameters)).ToList();

            return new Result { Success = true, Items = items };
        }
    }
}
