using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Onboarding;

public class SaveDefaultTerms
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
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
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.TermsAr) || !string.IsNullOrWhiteSpace(x.TermsEn))
                .WithMessage("At least one terms field is required.");
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
            var termsAr = NullIfBlank(request.TermsAr);
            var termsEn = NullIfBlank(request.TermsEn);

            var sql = """
                UPDATE WarrantyTemplate
                SET UpdatedAt = UTC_TIMESTAMP()
                """;

            if (termsAr != null)
                sql += ", TermsAr = @TermsAr";
            if (termsEn != null)
                sql += ", TermsEn = @TermsEn";

            sql += " WHERE ShopId = @ShopId AND Status = @Status";

            var updated = await db.ExecuteAsync(
                sql,
                new { request.ShopId, TermsAr = termsAr, TermsEn = termsEn, Status = TemplateStatuses.Active });

            return updated == 0
                ? new Result { Success = false, ErrorCode = ErrorCodes.NotFound }
                : new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
