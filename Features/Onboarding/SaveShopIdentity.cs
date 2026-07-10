using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Onboarding;

public class SaveShopIdentity
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? BusinessCategory { get; set; }
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
            RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
            RuleFor(x => x.Phone).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.Phone));
            RuleFor(x => x.City).MaximumLength(80).When(x => !string.IsNullOrWhiteSpace(x.City));
            RuleFor(x => x.BusinessCategory)
                .Must(x => BusinessCategories.Supported.Contains(x!))
                .When(x => !string.IsNullOrWhiteSpace(x.BusinessCategory));
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
            var updated = await db.ExecuteAsync(
                """
                UPDATE Shop
                SET Name = @Name,
                    Phone = @Phone,
                    City = @City,
                    BusinessCategory = @BusinessCategory,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @ShopId
                """,
                new
                {
                    request.ShopId,
                    Name = request.Name.Trim(),
                    Phone = NullIfBlank(request.Phone),
                    City = NullIfBlank(request.City),
                    BusinessCategory = NullIfBlank(request.BusinessCategory)
                });

            return updated == 0
                ? new Result { Success = false, ErrorCode = ErrorCodes.NotFound }
                : new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
