using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Onboarding;

public class GetOnboardingState
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? BusinessCategory { get; set; }
        public string? LogoPath { get; set; }
        public DateTime? OnboardingCompletedAt { get; set; }
        public int TemplatesCount { get; set; }
        public int SuggestedStep { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
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
            var shop = await db.QueryFirstOrDefaultAsync<ShopRow>(
                """
                SELECT Name, Phone, City, BusinessCategory, LogoPath, OnboardingCompletedAt
                FROM Shop
                WHERE Id = @ShopId
                """,
                new { request.ShopId });

            if (shop == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var templatesCount = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WarrantyTemplate WHERE ShopId = @ShopId AND Status = @Status",
                new { request.ShopId, Status = TemplateStatuses.Active });

            var terms = await db.QueryFirstOrDefaultAsync<TermsRow>(
                """
                SELECT TermsAr, TermsEn
                FROM WarrantyTemplate
                WHERE ShopId = @ShopId AND Status = @Status
                ORDER BY CreatedAt
                LIMIT 1
                """,
                new { request.ShopId, Status = TemplateStatuses.Active });

            return new Result
            {
                Success = true,
                Name = shop.Name,
                Phone = shop.Phone,
                City = shop.City,
                BusinessCategory = shop.BusinessCategory,
                LogoPath = shop.LogoPath,
                OnboardingCompletedAt = shop.OnboardingCompletedAt,
                TemplatesCount = templatesCount,
                SuggestedStep = SuggestStep(shop, templatesCount),
                TermsAr = terms?.TermsAr,
                TermsEn = terms?.TermsEn
            };
        }

        internal static int SuggestStep(ShopRow shop, int templatesCount)
        {
            if (shop.OnboardingCompletedAt != null)
                return 4;

            if (templatesCount > 0)
                return 3;

            if (!string.IsNullOrWhiteSpace(shop.City) || !string.IsNullOrWhiteSpace(shop.BusinessCategory))
                return 2;

            return 1;
        }

        internal sealed class ShopRow
        {
            public string Name { get; set; } = "";
            public string? Phone { get; set; }
            public string? City { get; set; }
            public string? BusinessCategory { get; set; }
            public string? LogoPath { get; set; }
            public DateTime? OnboardingCompletedAt { get; set; }
        }

        private sealed class TermsRow
        {
            public string? TermsAr { get; set; }
            public string? TermsEn { get; set; }
        }
    }
}
