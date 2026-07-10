using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Warranties;
using MediatR;

namespace DammaniAPI.Features.Customers;

// Customer history: warranties + service requests matched by phone (DMN-701).
public class GetCustomerHistory
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
    }

    public class WarrantyHistoryItem
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public string? Status { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class ServiceRequestHistoryItem
    {
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public CustomerInfo? Customer { get; set; }
        public List<WarrantyHistoryItem> Warranties { get; set; } = new();
        public List<ServiceRequestHistoryItem> ServiceRequests { get; set; } = new();
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
            var customer = await db.QueryFirstOrDefaultAsync<CustomerInfo>(
                """
                SELECT Id, Name, Phone
                FROM Customer
                WHERE Id = @CustomerId AND ShopId = @ShopId
                """,
                new { request.CustomerId, request.ShopId });

            if (customer == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var warranties = (await db.QueryAsync<WarrantyHistoryItem>(
                $"""
                SELECT w.Id, w.Code, w.ProductName,
                       {WarrantyListFilter.DerivedStatusSql} AS Status,
                       w.ExpiryDate
                FROM Warranty w
                WHERE w.CustomerId = @CustomerId AND w.ShopId = @ShopId
                ORDER BY w.CreatedAt DESC
                LIMIT 50
                """,
                new { request.CustomerId, request.ShopId })).ToList();

            var serviceRequests = (await db.QueryAsync<ServiceRequestHistoryItem>(
                """
                SELECT Id, RequestNumber, Status, CreatedAt
                FROM ServiceRequest
                WHERE CustomerPhone = @Phone AND ShopId = @ShopId
                ORDER BY CreatedAt DESC
                LIMIT 50
                """,
                new { Phone = customer.Phone, request.ShopId })).ToList();

            return new Result
            {
                Success = true,
                Customer = customer,
                Warranties = warranties,
                ServiceRequests = serviceRequests
            };
        }
    }
}
