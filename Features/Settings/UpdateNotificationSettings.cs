using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Settings;

public class UpdateNotificationSettings
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public bool EmailAlertsEnabled { get; set; }
        public string? AlertEmail { get; set; }
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
            RuleFor(x => x.AlertEmail)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(255)
                .When(x => x.EmailAlertsEnabled);
            RuleFor(x => x.AlertEmail)
                .EmailAddress()
                .MaximumLength(255)
                .When(x => !x.EmailAlertsEnabled && !string.IsNullOrWhiteSpace(x.AlertEmail));
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
                SET EmailAlertsEnabled = @EmailAlertsEnabled,
                    AlertEmail = @AlertEmail,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @ShopId
                """,
                new
                {
                    request.ShopId,
                    EmailAlertsEnabled = request.EmailAlertsEnabled ? 1 : 0,
                    AlertEmail = request.EmailAlertsEnabled
                        ? request.AlertEmail?.Trim().ToLowerInvariant()
                        : NullIfBlank(request.AlertEmail)
                });

            if (updated == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "shop", request.ShopId, "shop.notification_settings_updated", request.ActorUserId);

            return new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
