using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Settings;

public class UpdatePublicPageSettings
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string PublicLanguage { get; set; } = Languages.Arabic;
        public string PublicTheme { get; set; } = "default";
        public bool PublicShowAddress { get; set; } = true;
        public bool PublicShowWhatsApp { get; set; } = true;
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        private static readonly HashSet<string> Themes = new(StringComparer.OrdinalIgnoreCase) { "default" };

        public CommandValidator()
        {
            RuleFor(x => x.PublicLanguage).Must(x => Languages.Supported.Contains(x));
            RuleFor(x => x.PublicTheme).Must(x => Themes.Contains(x));
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
                SET PublicLanguage = @PublicLanguage,
                    PublicTheme = @PublicTheme,
                    PublicShowAddress = @PublicShowAddress,
                    PublicShowWhatsApp = @PublicShowWhatsApp,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @ShopId
                """,
                new
                {
                    request.ShopId,
                    request.PublicLanguage,
                    PublicTheme = request.PublicTheme.ToLowerInvariant(),
                    PublicShowAddress = request.PublicShowAddress ? 1 : 0,
                    PublicShowWhatsApp = request.PublicShowWhatsApp ? 1 : 0
                });

            if (updated == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "shop", request.ShopId, "shop.public_settings_updated", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
