using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Warranties;

public class LogShare
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string WarrantyId { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.WarrantyId).NotEmpty();
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

            var exists = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Warranty WHERE Id = @WarrantyId AND ShopId = @ShopId",
                new { request.WarrantyId, request.ShopId });

            if (exists == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            // Idempotent-tolerant: each share click may log again; checklist only needs EXISTS.
            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "warranty", request.WarrantyId,
                "warranty.shared", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
