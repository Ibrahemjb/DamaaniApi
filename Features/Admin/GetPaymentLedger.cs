using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetPaymentLedger
{
    public class Query : IRequest<Result>
    {
        public string? Status { get; set; }
        public string? Method { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class PaymentRow
    {
        public string Id { get; set; } = "";
        public string ShopId { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string PlanCode { get; set; } = "";
        public decimal AmountUsd { get; set; }
        public decimal AmountIls { get; set; }
        public string Method { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Reference { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<PaymentRow> Items { get; set; } = new();
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
            var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();
            var method = string.IsNullOrWhiteSpace(request.Method) ? null : request.Method.Trim();

            using var db = _mdb.Open();
            var total = await db.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM Payment pay
                WHERE (@Status IS NULL OR pay.Status = @Status)
                  AND (@Method IS NULL OR pay.Method = @Method)
                """,
                new { Status = status, Method = method });

            var items = (await db.QueryAsync<PaymentRow>(
                """
                SELECT pay.Id, pay.ShopId, s.Name AS ShopName, p.Code AS PlanCode,
                       pay.AmountUsd, pay.AmountIls, pay.Method, pay.Status, pay.Reference, pay.PaidAt
                FROM Payment pay
                JOIN Shop s ON s.Id = pay.ShopId
                JOIN Plan p ON p.Id = pay.PlanId
                WHERE (@Status IS NULL OR pay.Status = @Status)
                  AND (@Method IS NULL OR pay.Method = @Method)
                ORDER BY pay.PaidAt DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                new { Status = status, Method = method, PageSize = pageSize, Offset = offset })).ToList();

            return new Result { Items = items, Total = total, Page = page, PageSize = pageSize };
        }
    }
}
