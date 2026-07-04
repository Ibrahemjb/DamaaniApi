using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Templates;

public class CreateTemplate
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
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
        public string? Id { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Category).Must(x => BusinessCategories.Supported.Contains(x));
            RuleFor(x => x.DurationMonths).InclusiveBetween(1, 120);
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

            var id = Guid.NewGuid().ToString();
            using var db = _mdb.Open();
            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    INSERT INTO WarrantyTemplate
                        (Id, ShopId, Name, Category, DurationMonths, TermsAr, TermsEn,
                         ExclusionsAr, ExclusionsEn, ServiceInstructionsAr, ServiceInstructionsEn, Status)
                    VALUES
                        (@Id, @ShopId, @Name, @Category, @DurationMonths, @TermsAr, @TermsEn,
                         @ExclusionsAr, @ExclusionsEn, @ServiceInstructionsAr, @ServiceInstructionsEn, @Status)
                    """,
                    new
                    {
                        Id = id,
                        request.ShopId,
                        Name = request.Name.Trim(),
                        request.Category,
                        request.DurationMonths,
                        TermsAr = NullIfBlank(request.TermsAr),
                        TermsEn = NullIfBlank(request.TermsEn),
                        ExclusionsAr = NullIfBlank(request.ExclusionsAr),
                        ExclusionsEn = NullIfBlank(request.ExclusionsEn),
                        ServiceInstructionsAr = NullIfBlank(request.ServiceInstructionsAr),
                        ServiceInstructionsEn = NullIfBlank(request.ServiceInstructionsEn),
                        Status = TemplateStatuses.Active
                    },
                    tx);
                await ActivityLogger.LogAsync(db, tx, request.ShopId, "template", id, "template.created", request.ActorUserId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, Id = id };
        }

        internal static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
