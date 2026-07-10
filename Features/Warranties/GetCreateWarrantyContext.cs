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
        public int? DefaultWarrantyDurationMonths { get; set; }
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

            var shopRow = await db.QueryFirstOrDefaultAsync<(string? Name, string? PublicLanguage, int? DefaultWarrantyDurationMonths)?>(
                """
                SELECT Name, PublicLanguage, DefaultWarrantyDurationMonths
                FROM Shop WHERE Id = @ShopId
                """,
                new { request.ShopId });

            var templates = (await db.QueryAsync<TemplateOption>(
                """
                SELECT Id, Name, Category, DurationMonths
                FROM WarrantyTemplate
                WHERE ShopId = @ShopId AND Status = @Active
                ORDER BY Name
                """,
                new { request.ShopId, Active = TemplateStatuses.Active })).ToList();

            var branches = (await db.QueryAsync<BranchOption>(
                """
                SELECT Id, Name FROM Branch
                WHERE ShopId = @ShopId AND Status = @Active
                ORDER BY Name
                """,
                new { request.ShopId, Active = BranchStatuses.Active })).ToList();

            var usage = await WarrantyUsage.GetForShopAsync(db, null, request.ShopId);

            return new Result
            {
                Success = true,
                Templates = templates,
                Branches = branches,
                Usage = new UsageState { Used = usage.Used, Limit = usage.Limit, Blocked = usage.Blocked },
                DefaultLanguage = shopRow?.PublicLanguage ?? Languages.Arabic,
                DefaultWarrantyDurationMonths = shopRow?.DefaultWarrantyDurationMonths,
                ShopName = shopRow?.Name
            };
        }
    }
}
