using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// Warranty detail: core + customer + derived status + activity timeline
// (DMN-406). Cross-shop/unknown ids return not_found — no existence leak.
public class GetWarranty
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? WarrantyId { get; set; }
    }

    public class CustomerInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
    }

    public class ServiceRequestItem
    {
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ActivityItem
    {
        public string? Action { get; set; }
        public string? ActorName { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WarrantyDetail
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? PublicSlug { get; set; }
        public string? Status { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? ColorSpecs { get; set; }
        public string? PurchaseReference { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public int? DurationMonths { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? TemplateId { get; set; }
        public string? CustomerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public WarrantyDetail? Warranty { get; set; }
        public CustomerInfo? Customer { get; set; }
        public string? PublicUrl { get; set; }
        // For WhatsApp {shop_name} fill (DMN-404) and print layouts (DMN-408).
        public string? ShopName { get; set; }
        public List<ServiceRequestItem> ServiceRequests { get; set; } = new();
        public List<ActivityItem> Activity { get; set; } = new();
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

            using var db = _mdb.Open();
            var warranty = await db.QueryFirstOrDefaultAsync<WarrantyDetail>(
                """
                SELECT Id, Code, PublicSlug,
                       CASE WHEN Status = @Active AND ExpiryDate IS NOT NULL AND ExpiryDate < CURDATE()
                            THEN 'expired' ELSE Status END AS Status,
                       ProductName, Category, Model, SerialNumber, ColorSpecs, PurchaseReference,
                       PurchaseDate, DurationMonths, ExpiryDate, TermsAr, TermsEn,
                       CancelReason, CancelledAt, TemplateId, CustomerId, CreatedAt, UpdatedAt
                FROM Warranty
                WHERE Id = @WarrantyId AND ShopId = @ShopId
                """,
                new { request.WarrantyId, request.ShopId, Active = WarrantyStatuses.Active });

            if (warranty == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var customer = await db.QueryFirstOrDefaultAsync<CustomerInfo>(
                "SELECT Id, Name, Phone, City, Address, Notes FROM Customer WHERE Id = @CustomerId",
                new { warranty.CustomerId });

            var shopName = await db.ExecuteScalarAsync<string?>(
                "SELECT Name FROM Shop WHERE Id = @ShopId", new { request.ShopId });

            var serviceRequests = (await db.QueryAsync<ServiceRequestItem>(
                """
                SELECT Id, RequestNumber, Status, CreatedAt
                FROM ServiceRequest
                WHERE WarrantyId = @WarrantyId AND ShopId = @ShopId
                ORDER BY CreatedAt DESC
                LIMIT 20
                """,
                new { request.WarrantyId, request.ShopId })).ToList();

            var activity = (await db.QueryAsync<ActivityItem>(
                """
                SELECT a.Action, u.FullName AS ActorName, a.Details, a.CreatedAt
                FROM ActivityLog a
                LEFT JOIN User u ON u.Id = a.ActorUserId
                WHERE a.EntityType = 'warranty' AND a.EntityId = @WarrantyId AND a.ShopId = @ShopId
                ORDER BY a.CreatedAt DESC
                LIMIT 50
                """,
                new { request.WarrantyId, request.ShopId })).ToList();

            return new Result
            {
                Success = true,
                Warranty = warranty,
                Customer = customer,
                ShopName = shopName,
                PublicUrl = CreateWarranty.CommandHandler.BuildPublicUrl(_configuration, warranty.PublicSlug!),
                ServiceRequests = serviceRequests,
                Activity = activity
            };
        }
    }
}
