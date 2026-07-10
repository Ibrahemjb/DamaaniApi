using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Warranties;
using MediatR;

namespace DammaniAPI.Features.Customers;

// Full customer detail for the dedicated customer page: profile, stats,
// warranties, distinct products, and service requests matched by phone.
public class GetCustomerDetail
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? CustomerId { get; set; }
    }

    public class CustomerInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CustomerStats
    {
        public int WarrantyCount { get; set; }
        public int ActiveWarrantyCount { get; set; }
        public int OpenRequestCount { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }

    public class WarrantyItem
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? SerialNumber { get; set; }
        public string? Status { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductItem
    {
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? SerialNumber { get; set; }
        public int WarrantyCount { get; set; }
        public string? LatestWarrantyId { get; set; }
        public string? LatestWarrantyCode { get; set; }
    }

    public class ServiceRequestItem
    {
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public string? ProductName { get; set; }
        public string? WarrantyCode { get; set; }
        public string? WarrantyId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public CustomerInfo? Customer { get; set; }
        public CustomerStats Stats { get; set; } = new();
        public List<WarrantyItem> Warranties { get; set; } = new();
        public List<ProductItem> Products { get; set; } = new();
        public List<ServiceRequestItem> ServiceRequests { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            if (string.IsNullOrWhiteSpace(request.CustomerId))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            using var db = _mdb.Open();
            var customer = await db.QueryFirstOrDefaultAsync<CustomerInfo>(
                """
                SELECT Id, Name, Phone, City, Address, Notes, CreatedAt, UpdatedAt
                FROM Customer
                WHERE Id = @CustomerId AND ShopId = @ShopId
                """,
                new { request.CustomerId, request.ShopId });

            if (customer == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var warranties = (await db.QueryAsync<WarrantyItem>(
                $"""
                SELECT w.Id, w.Code, w.ProductName, w.Category, w.SerialNumber,
                       {WarrantyListFilter.DerivedStatusSql} AS Status,
                       w.PurchaseDate, w.ExpiryDate, w.CreatedAt
                FROM Warranty w
                WHERE w.CustomerId = @CustomerId AND w.ShopId = @ShopId
                ORDER BY w.CreatedAt DESC
                LIMIT 100
                """,
                new { request.CustomerId, request.ShopId })).ToList();

            var products = (await db.QueryAsync<ProductItem>(
                """
                SELECT w.ProductName,
                       MAX(w.Category) AS Category,
                       MAX(w.SerialNumber) AS SerialNumber,
                       COUNT(*) AS WarrantyCount,
                       SUBSTRING_INDEX(GROUP_CONCAT(w.Id ORDER BY w.CreatedAt DESC), ',', 1) AS LatestWarrantyId,
                       SUBSTRING_INDEX(GROUP_CONCAT(w.Code ORDER BY w.CreatedAt DESC), ',', 1) AS LatestWarrantyCode
                FROM Warranty w
                WHERE w.CustomerId = @CustomerId AND w.ShopId = @ShopId
                  AND w.Status <> 'draft'
                GROUP BY w.ProductName
                ORDER BY MAX(w.CreatedAt) DESC
                LIMIT 50
                """,
                new { request.CustomerId, request.ShopId })).ToList();

            var serviceRequests = (await db.QueryAsync<ServiceRequestItem>(
                """
                SELECT sr.Id, sr.RequestNumber, sr.Status,
                       COALESCE(w.ProductName, '') AS ProductName,
                       w.Code AS WarrantyCode,
                       sr.WarrantyId,
                       sr.CreatedAt
                FROM ServiceRequest sr
                LEFT JOIN Warranty w ON w.Id = sr.WarrantyId
                WHERE sr.CustomerPhone = @Phone AND sr.ShopId = @ShopId
                ORDER BY sr.CreatedAt DESC
                LIMIT 100
                """,
                new { Phone = customer.Phone, request.ShopId })).ToList();

            var warrantyCount = warranties.Count(w => w.Status != "draft");
            var activeWarrantyCount = warranties.Count(w => w.Status == "active");
            var openRequestCount = serviceRequests.Count(r => r.Status != "closed");

            DateTime? lastActivity = null;
            var warrantyLatest = warranties.Select(w => (DateTime?)w.CreatedAt).DefaultIfEmpty(null).Max();
            var requestLatest = serviceRequests.Select(r => (DateTime?)r.CreatedAt).DefaultIfEmpty(null).Max();
            if (warrantyLatest.HasValue || requestLatest.HasValue)
            {
                lastActivity = warrantyLatest.HasValue && requestLatest.HasValue
                    ? (warrantyLatest > requestLatest ? warrantyLatest : requestLatest)
                    : warrantyLatest ?? requestLatest;
            }

            return new Result
            {
                Success = true,
                Customer = customer,
                Stats = new CustomerStats
                {
                    WarrantyCount = warrantyCount,
                    ActiveWarrantyCount = activeWarrantyCount,
                    OpenRequestCount = openRequestCount,
                    LastActivityAt = lastActivity
                },
                Warranties = warranties,
                Products = products,
                ServiceRequests = serviceRequests
            };
        }
    }
}
