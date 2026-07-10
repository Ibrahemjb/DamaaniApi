using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// Warranty list with multi-field search, the BP §10.11 filter set, derived
// expired status, and pagination (DMN-405).
public class GetWarranties
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? ExpiryFrom { get; set; }
        public DateTime? ExpiryTo { get; set; }
        public string? BranchId { get; set; }
        public string? CreatedByUserId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class WarrantyListItem
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? ProductName { get; set; }
        public string? SerialNumber { get; set; }
        public string? Status { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
        // Per-row WhatsApp share needs the public link without a detail fetch.
        public string? PublicUrl { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<WarrantyListItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        // For the {shop_name} variable in row-level share messages (DMN-410).
        public string? ShopName { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IConfiguration _configuration;

        public QueryHandler(IManagementDatabase mdb, IConfiguration configuration)
        {
            _mdb = mdb;
            _configuration = configuration;
        }

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var (whereSql, parameters) = WarrantyListFilter.Build(request.ShopId, new WarrantyListFilter.Args
            {
                Search = request.Search,
                Status = request.Status,
                Category = request.Category,
                CreatedFrom = request.CreatedFrom,
                CreatedTo = request.CreatedTo,
                ExpiryFrom = request.ExpiryFrom,
                ExpiryTo = request.ExpiryTo,
                BranchId = request.BranchId,
                CreatedByUserId = request.CreatedByUserId
            });

            using var db = _mdb.Open();
            var totalCount = await db.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Warranty w JOIN Customer c ON c.Id = w.CustomerId WHERE {whereSql}",
                parameters);

            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);
            var items = (await db.QueryAsync<WarrantyListItem>(
                $"""
                SELECT w.Id, w.Code, c.Name AS CustomerName, c.Phone AS CustomerPhone,
                       w.ProductName, w.SerialNumber,
                       {WarrantyListFilter.DerivedStatusSql} AS Status,
                       w.ExpiryDate, w.CreatedAt, w.PublicSlug AS PublicUrl
                FROM Warranty w
                JOIN Customer c ON c.Id = w.CustomerId
                WHERE {whereSql}
                ORDER BY w.CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                parameters)).ToList();
            foreach (var item in items)
                item.PublicUrl = CreateWarranty.CommandHandler.BuildPublicUrl(_configuration, item.PublicUrl!);

            var shopName = await db.ExecuteScalarAsync<string?>(
                "SELECT Name FROM Shop WHERE Id = @ShopId", new { request.ShopId });

            return new Result
            {
                Success = true,
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                ShopName = shopName
            };
        }
    }
}
