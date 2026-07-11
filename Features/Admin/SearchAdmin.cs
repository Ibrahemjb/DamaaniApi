using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class SearchAdmin
{
    public class Query : IRequest<Result>
    {
        public string Q { get; set; } = "";
    }

    public class ShopHit
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? OwnerEmail { get; set; }
        public string Status { get; set; } = "";
        public string PlanCode { get; set; } = "";
    }

    public class WarrantyHit
    {
        public string Id { get; set; } = "";
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string Code { get; set; } = "";
        public string? SerialNumber { get; set; }
        public string ProductName { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class ServiceRequestHit
    {
        public string Id { get; set; } = "";
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string RequestNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public string CustomerName { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<ShopHit> Shops { get; set; } = new();
        public List<WarrantyHit> Warranties { get; set; } = new();
        public List<ServiceRequestHit> ServiceRequests { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            var q = request.Q?.Trim() ?? "";
            if (q.Length < 2)
                return new Result();

            var like = $"%{q}%";
            using var db = _mdb.Open();

            var shops = (await db.QueryAsync<ShopHit>(
                """
                SELECT s.Id, s.Name, owner.Email AS OwnerEmail, s.Status, COALESCE(p.Code, 'free') AS PlanCode
                FROM Shop s
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                LEFT JOIN ShopUser su ON su.ShopId = s.Id AND su.Role = 'owner' AND su.Status = 'active'
                LEFT JOIN User owner ON owner.Id = su.UserId
                WHERE s.Name LIKE @Like OR owner.Email LIKE @Like
                ORDER BY s.CreatedAt DESC
                LIMIT 20
                """,
                new { Like = like })).ToList();

            var warranties = (await db.QueryAsync<WarrantyHit>(
                """
                SELECT w.Id, w.ShopId, s.Name AS ShopName, w.Code, w.SerialNumber, w.ProductName, w.Status
                FROM Warranty w
                JOIN Shop s ON s.Id = w.ShopId
                WHERE w.Code LIKE @Like OR w.SerialNumber LIKE @Like OR w.PublicSlug LIKE @Like
                ORDER BY w.CreatedAt DESC
                LIMIT 20
                """,
                new { Like = like })).ToList();

            var requests = (await db.QueryAsync<ServiceRequestHit>(
                """
                SELECT sr.Id, sr.ShopId, s.Name AS ShopName, sr.RequestNumber, sr.Status, sr.CustomerName
                FROM ServiceRequest sr
                JOIN Shop s ON s.Id = sr.ShopId
                WHERE sr.RequestNumber LIKE @Like OR sr.Id = @Exact OR sr.CustomerPhone LIKE @Like
                ORDER BY sr.CreatedAt DESC
                LIMIT 20
                """,
                new { Like = like, Exact = q })).ToList();

            return new Result { Shops = shops, Warranties = warranties, ServiceRequests = requests };
        }
    }
}
