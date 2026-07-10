using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetDefaultTemplates
{
    public class Query : IRequest<Result> { }

    public class TemplateRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int DurationMonths { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
        public string? ExclusionsAr { get; set; }
        public string? ExclusionsEn { get; set; }
        public string? ServiceInstructionsAr { get; set; }
        public string? ServiceInstructionsEn { get; set; }
        public int SortOrder { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<TemplateRow> Items { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var items = (await db.QueryAsync<TemplateRow>(
                """
                SELECT Id, Name, Category, DurationMonths, TermsAr, TermsEn,
                       ExclusionsAr, ExclusionsEn, ServiceInstructionsAr, ServiceInstructionsEn, SortOrder
                FROM DefaultTemplate
                ORDER BY SortOrder, Name
                """)).ToList();
            return new Result { Items = items };
        }
    }
}

public class UpdateDefaultTemplate
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int DurationMonths { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
        public string? ExclusionsAr { get; set; }
        public string? ExclusionsEn { get; set; }
        public string? ServiceInstructionsAr { get; set; }
        public string? ServiceInstructionsEn { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.DurationMonths).InclusiveBetween(1, 120);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var rows = await db.ExecuteAsync(
                """
                UPDATE DefaultTemplate
                SET Name = @Name,
                    DurationMonths = @DurationMonths,
                    TermsAr = @TermsAr,
                    TermsEn = @TermsEn,
                    ExclusionsAr = @ExclusionsAr,
                    ExclusionsEn = @ExclusionsEn,
                    ServiceInstructionsAr = @ServiceInstructionsAr,
                    ServiceInstructionsEn = @ServiceInstructionsEn,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @Id
                """,
                request);
            if (rows == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, null, "default_template", request.Id, "content.default_template_updated",
                request.ActorUserId, $"{{\"name\":\"{SuspendShop.CommandHandler.EscapeJson(request.Name)}\"}}");

            return new Result { Success = true };
        }
    }
}
