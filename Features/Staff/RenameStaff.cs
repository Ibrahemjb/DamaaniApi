using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class RenameStaff
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
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
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
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
            var belongs = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ShopUser WHERE ShopId = @ShopId AND UserId = @UserId",
                new { request.ShopId, request.UserId });
            if (belongs == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await db.ExecuteAsync(
                "UPDATE User SET FullName = @FullName, UpdatedAt = UTC_TIMESTAMP() WHERE Id = @UserId",
                new { request.UserId, FullName = request.FullName.Trim() });

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "user", request.UserId, "staff.renamed", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
