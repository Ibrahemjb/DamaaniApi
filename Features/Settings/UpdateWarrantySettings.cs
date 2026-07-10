using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Settings;

public class UpdateWarrantySettings
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public int? DefaultWarrantyDurationMonths { get; set; }
        public bool AllowExpiredRequests { get; set; } = true;
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
            RuleFor(x => x.DefaultWarrantyDurationMonths)
                .InclusiveBetween(1, 120)
                .When(x => x.DefaultWarrantyDurationMonths.HasValue);
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
                SET DefaultWarrantyDurationMonths = @DefaultWarrantyDurationMonths,
                    AllowExpiredRequests = @AllowExpiredRequests,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @ShopId
                """,
                new
                {
                    request.ShopId,
                    request.DefaultWarrantyDurationMonths,
                    AllowExpiredRequests = request.AllowExpiredRequests ? 1 : 0
                });

            if (updated == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "shop", request.ShopId, "shop.warranty_settings_updated", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
