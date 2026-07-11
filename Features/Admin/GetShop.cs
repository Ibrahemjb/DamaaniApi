using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetShop
{
    public class Query : IRequest<Result>
    {
        public string ShopId { get; set; } = "";
    }

    public class Counts
    {
        public int Warranties { get; set; }
        public int ServiceRequests { get; set; }
        public int Staff { get; set; }
    }

    public class PaymentRow
    {
        public string Id { get; set; } = "";
        public string PlanCode { get; set; } = "";
        public decimal AmountUsd { get; set; }
        public decimal AmountIls { get; set; }
        public string Method { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Reference { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class ActivityRow
    {
        public string Action { get; set; } = "";
        public string? ActorUserId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? OwnerEmail { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Status { get; set; }
        public string? SuspensionNote { get; set; }
        public string PlanCode { get; set; } = "";
        public string PlanNameEn { get; set; } = "";
        public string PlanNameAr { get; set; } = "";
        public int Used { get; set; }
        public int Limit { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Counts Counts { get; set; } = new();
        public List<PaymentRow> Payments { get; set; } = new();
        public List<ActivityRow> Activity { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            using var db = _mdb.Open();
            var shop = await db.QueryFirstOrDefaultAsync<Result>(
                """
                SELECT
                    s.Id,
                    s.Name,
                    owner.Email AS OwnerEmail,
                    s.Country,
                    s.City,
                    s.Status,
                    s.SuspensionNote,
                    COALESCE(p.Code, 'free') AS PlanCode,
                    COALESCE(p.NameEn, 'Free') AS PlanNameEn,
                    COALESCE(p.NameAr, 'المجانية') AS PlanNameAr,
                    sub.CurrentPeriodEnd,
                    s.CreatedAt
                FROM Shop s
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                LEFT JOIN ShopUser su ON su.ShopId = s.Id AND su.Role = 'owner' AND su.Status = 'active'
                LEFT JOIN User owner ON owner.Id = su.UserId
                WHERE s.Id = @ShopId
                """,
                new { request.ShopId });

            if (shop is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var usage = await UsageService.GetUsageAsync(db, null, request.ShopId);
            shop.Used = usage.Used;
            shop.Limit = usage.Limit;
            shop.Success = true;

            shop.Counts = await db.QueryFirstAsync<Counts>(
                """
                SELECT
                    (SELECT COUNT(*) FROM Warranty WHERE ShopId = @ShopId AND Status <> @Draft) AS Warranties,
                    (SELECT COUNT(*) FROM ServiceRequest WHERE ShopId = @ShopId) AS ServiceRequests,
                    (SELECT COUNT(*) FROM ShopUser WHERE ShopId = @ShopId AND Status = @Active) AS Staff
                """,
                new { request.ShopId, Draft = WarrantyStatuses.Draft, Active = UserStatuses.Active });

            shop.Payments = (await db.QueryAsync<PaymentRow>(
                """
                SELECT pay.Id, p.Code AS PlanCode, pay.AmountUsd, pay.AmountIls,
                       pay.Method, pay.Status, pay.Reference, pay.PaidAt
                FROM Payment pay
                JOIN Plan p ON p.Id = pay.PlanId
                WHERE pay.ShopId = @ShopId
                ORDER BY pay.PaidAt DESC
                LIMIT 20
                """,
                new { request.ShopId })).ToList();

            shop.Activity = (await db.QueryAsync<ActivityRow>(
                """
                SELECT Action, ActorUserId, Details, CreatedAt
                FROM ActivityLog
                WHERE ShopId = @ShopId
                ORDER BY CreatedAt DESC
                LIMIT 20
                """,
                new { request.ShopId })).ToList();

            return shop;
        }
    }
}
