using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Onboarding;

public class SelectCategories
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string[] Categories { get; set; } = [];
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<TemplateItem> Templates { get; set; } = [];
    }

    public class TemplateItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int DurationMonths { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Categories).NotEmpty();
            RuleForEach(x => x.Categories).Must(x => OnboardingCategories.Supported.Contains(x));
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var categories = request.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (categories.Length == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();

            var existing = (await db.QueryAsync<NameCategoryRow>(
                """
                SELECT Name, Category
                FROM WarrantyTemplate
                WHERE ShopId = @ShopId
                """,
                new { request.ShopId })).ToHashSet();

            foreach (var category in categories)
            {
                await db.ExecuteAsync(
                    """
                    INSERT INTO WarrantyTemplate
                        (Id, ShopId, Name, Category, DurationMonths, TermsAr, TermsEn,
                         ExclusionsAr, ExclusionsEn, ServiceInstructionsAr, ServiceInstructionsEn, Status, CreatedAt)
                    SELECT
                        UUID(), @ShopId, dt.Name, dt.Category, dt.DurationMonths, dt.TermsAr, dt.TermsEn,
                        dt.ExclusionsAr, dt.ExclusionsEn, dt.ServiceInstructionsAr, dt.ServiceInstructionsEn,
                        @Status, UTC_TIMESTAMP()
                    FROM DefaultTemplate dt
                    WHERE dt.Category = @Category
                      AND NOT EXISTS (
                          SELECT 1 FROM WarrantyTemplate wt
                          WHERE wt.ShopId = @ShopId AND wt.Name = dt.Name AND wt.Category = dt.Category
                      )
                    """,
                    new { request.ShopId, Category = category, Status = TemplateStatuses.Active });
            }

            var templates = await db.QueryAsync<TemplateItem>(
                """
                SELECT Id, Name, Category, DurationMonths
                FROM WarrantyTemplate
                WHERE ShopId = @ShopId AND Category IN @Categories AND Status = @Status
                ORDER BY Category, Name
                """,
                new { request.ShopId, Categories = categories, Status = TemplateStatuses.Active });

            var created = templates
                .Where(t => !existing.Contains(new NameCategoryRow { Name = t.Name, Category = t.Category }))
                .ToList();

            return new Result { Success = true, Templates = created };
        }

        private sealed class NameCategoryRow : IEquatable<NameCategoryRow>
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";

            public bool Equals(NameCategoryRow? other)
                => other != null
                   && string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && string.Equals(Category, other.Category, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object? obj) => Equals(obj as NameCategoryRow);

            public override int GetHashCode()
                => HashCode.Combine(Name, Category.ToLowerInvariant());
        }
    }
}

public static class OnboardingCategories
{
    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        BusinessCategories.SolarBattery,
        BusinessCategories.MobileElectronics,
        BusinessCategories.Appliances,
        BusinessCategories.FurnitureTools
    };
}
