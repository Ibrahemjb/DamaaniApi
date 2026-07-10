using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class GetPayments
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class PaymentRow
    {
        public string Id { get; set; } = "";
        public string PlanCode { get; set; } = "";
        public string PlanNameEn { get; set; } = "";
        public string PlanNameAr { get; set; } = "";
        public decimal AmountUsd { get; set; }
        public decimal AmountIls { get; set; }
        public string Method { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Reference { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public IReadOnlyList<PaymentRow> Payments { get; set; } = Array.Empty<PaymentRow>();
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
            var rows = await db.QueryAsync<PaymentRow>(
                """
                SELECT p.Id, pl.Code AS PlanCode, pl.NameEn AS PlanNameEn, pl.NameAr AS PlanNameAr,
                       p.AmountUsd, p.AmountIls, p.Method, p.Status, p.Reference, p.PaidAt
                FROM Payment p
                JOIN Plan pl ON pl.Id = p.PlanId
                WHERE p.ShopId = @ShopId
                ORDER BY p.PaidAt DESC
                """,
                new { ShopId = request.ShopId });

            return new Result { Success = true, Payments = rows.AsList() };
        }
    }
}
