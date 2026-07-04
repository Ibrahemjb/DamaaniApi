using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Templates;

public class UpdateTemplate
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string TemplateId { get; set; } = "";
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
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.TemplateId).NotEmpty();
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

            using var db = _mdb.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Warranties created before this edit keep their original terms:
                // terms are snapshotted onto the warranty at creation (BP §18).
                var affected = await db.ExecuteAsync(
                    """
                    UPDATE WarrantyTemplate
                    SET Name = @Name, Category = @Category, DurationMonths = @DurationMonths,
                        TermsAr = @TermsAr, TermsEn = @TermsEn,
                        ExclusionsAr = @ExclusionsAr, ExclusionsEn = @ExclusionsEn,
                        ServiceInstructionsAr = @ServiceInstructionsAr, ServiceInstructionsEn = @ServiceInstructionsEn,
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @TemplateId AND ShopId = @ShopId
                    """,
                    new
                    {
                        request.TemplateId,
                        request.ShopId,
                        Name = request.Name.Trim(),
                        request.Category,
                        request.DurationMonths,
                        TermsAr = CreateTemplate.CommandHandler.NullIfBlank(request.TermsAr),
                        TermsEn = CreateTemplate.CommandHandler.NullIfBlank(request.TermsEn),
                        ExclusionsAr = CreateTemplate.CommandHandler.NullIfBlank(request.ExclusionsAr),
                        ExclusionsEn = CreateTemplate.CommandHandler.NullIfBlank(request.ExclusionsEn),
                        ServiceInstructionsAr = CreateTemplate.CommandHandler.NullIfBlank(request.ServiceInstructionsAr),
                        ServiceInstructionsEn = CreateTemplate.CommandHandler.NullIfBlank(request.ServiceInstructionsEn)
                    },
                    tx);

                if (affected == 0)
                {
                    tx.Rollback();
                    return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                }

                await ActivityLogger.LogAsync(db, tx, request.ShopId, "template", request.TemplateId, "template.updated", request.ActorUserId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true };
        }
    }
}
