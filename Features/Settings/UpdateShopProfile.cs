using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Settings;

public class UpdateShopProfile
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? BusinessCategory { get; set; }
        public bool RemoveLogo { get; set; }
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
            RuleFor(x => x.WhatsAppNumber)
                .MaximumLength(32)
                .Matches(@"^\+?[0-9\s\-()]*$")
                .When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber));
            RuleFor(x => x.Address).MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Address));
            RuleFor(x => x.City).MaximumLength(80).When(x => !string.IsNullOrWhiteSpace(x.City));
            RuleFor(x => x.Country).Length(2).When(x => !string.IsNullOrWhiteSpace(x.Country));
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
            var logoPath = request.RemoveLogo ? null : (string?)null;
            var updated = await db.ExecuteAsync(
                """
                UPDATE Shop
                SET Name = @Name,
                    Phone = @Phone,
                    WhatsAppNumber = @WhatsAppNumber,
                    Address = @Address,
                    City = @City,
                    Country = COALESCE(@Country, Country),
                    BusinessCategory = @BusinessCategory,
                    LogoPath = CASE WHEN @RemoveLogo = 1 THEN NULL ELSE LogoPath END,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @ShopId
                """,
                new
                {
                    request.ShopId,
                    Name = request.Name.Trim(),
                    Phone = NullIfBlank(request.Phone),
                    WhatsAppNumber = NullIfBlank(request.WhatsAppNumber),
                    Address = NullIfBlank(request.Address),
                    City = NullIfBlank(request.City),
                    Country = NullIfBlank(request.Country),
                    BusinessCategory = NullIfBlank(request.BusinessCategory),
                    RemoveLogo = request.RemoveLogo ? 1 : 0
                });

            if (updated == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "shop", request.ShopId, "shop.profile_updated", request.ActorUserId);

            return new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
