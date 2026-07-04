using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Templates;

public class GetTemplate
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? TemplateId { get; set; }
    }

    public class TemplateDetails
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public int DurationMonths { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
        public string? ExclusionsAr { get; set; }
        public string? ExclusionsEn { get; set; }
        public string? ServiceInstructionsAr { get; set; }
        public string? ServiceInstructionsEn { get; set; }
        public string? Status { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public TemplateDetails? Template { get; set; }
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
            var template = await db.QueryFirstOrDefaultAsync<TemplateDetails>(
                """
                SELECT Id, Name, Category, DurationMonths,
                       TermsAr, TermsEn, ExclusionsAr, ExclusionsEn,
                       ServiceInstructionsAr, ServiceInstructionsEn,
                       Status, LastUsedAt
                FROM WarrantyTemplate
                WHERE Id = @TemplateId AND ShopId = @ShopId
                """,
                new { request.TemplateId, request.ShopId });

            if (template == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            return new Result { Success = true, Template = template };
        }
    }
}
