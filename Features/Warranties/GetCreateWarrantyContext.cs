using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// Powers the create-warranty form (DMN-403): active templates for autofill,
// branches (empty until DMN-901/905), monthly usage state for the limit-blocked
// UI, and the shop default language for terms-tab ordering.
public class GetCreateWarrantyContext
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class TemplateOption
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public int DurationMonths { get; set; }
    }

    public class BranchOption
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class UsageState
    {
        public int Used { get; set; }
        public int Limit { get; set; }
        public bool Blocked { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<TemplateOption> Templates { get; set; } = new();
        public List<BranchOption> Branches { get; set; } = new();
        public UsageState Usage { get; set; } = new();
        public string DefaultLanguage { get; set; } = Languages.Arabic;
        // For the live public-card preview header (DMN-403).
        public string? ShopName { get; set; }
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

            var shopName = await db.ExecuteScalarAsync<string?>(
                "SELECT Name FROM Shop WHERE Id = @ShopId", new { request.ShopId });

            var templates = (await db.QueryAsync<TemplateOption>(
                """
                SELECT Id, Name, Category, DurationMonths
                FROM WarrantyTemplate
                WHERE ShopId = @ShopId AND Status = @Active
                ORDER BY Name
                """,
                new { request.ShopId, Active = TemplateStatuses.Active })).ToList();

            var usage = await WarrantyUsage.GetForShopAsync(db, null, request.ShopId);

            return new Result
            {
                Success = true,
                Templates = templates,
                // Branches ship with DMN-901/905; empty keeps the form's branch
                // selector hidden until then (documented contract for DMN-403).
                Branches = new List<BranchOption>(),
                Usage = new UsageState { Used = usage.Used, Limit = usage.Limit, Blocked = usage.Blocked },
                // Shop public default language column arrives with DMN-902;
                // Arabic is the product default until then (BP: Palestine-first).
                DefaultLanguage = Languages.Arabic,
                ShopName = shopName
            };
        }
    }
}
