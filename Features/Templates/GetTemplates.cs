using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Templates;

public class GetTemplates
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public bool IncludeInactive { get; set; }
    }

    public class TemplateListItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public int DurationMonths { get; set; }
        public string? Status { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool HasAr { get; set; }
        public bool HasEn { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<TemplateListItem> Templates { get; set; } = new();
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
            var templates = await db.QueryAsync<TemplateListItem>(
                """
                SELECT Id, Name, Category, DurationMonths, Status, LastUsedAt,
                       (TermsAr IS NOT NULL AND TermsAr <> '') AS HasAr,
                       (TermsEn IS NOT NULL AND TermsEn <> '') AS HasEn
                FROM WarrantyTemplate
                WHERE ShopId = @ShopId AND (@IncludeInactive OR Status = @Active)
                ORDER BY Name
                """,
                new { request.ShopId, request.IncludeInactive, Active = TemplateStatuses.Active });

            return new Result { Success = true, Templates = templates.ToList() };
        }
    }
}
