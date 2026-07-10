using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class SetStaffStatus
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string UserId { get; set; } = "";
        public bool Enabled { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.UserId).NotEmpty();
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ActorUserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            if (string.Equals(request.UserId, request.ActorUserId, StringComparison.OrdinalIgnoreCase))
                return new Result { Success = false, ErrorCode = ErrorCodes.Forbidden };

            using var db = _mdb.Open();
            var member = await db.QueryFirstOrDefaultAsync<(string Role, string Status)?>(
                "SELECT Role, Status FROM ShopUser WHERE ShopId = @ShopId AND UserId = @UserId",
                new { request.ShopId, request.UserId });

            if (member == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            if (string.Equals(member.Value.Role, Roles.Owner, StringComparison.OrdinalIgnoreCase))
                return new Result { Success = false, ErrorCode = ErrorCodes.Forbidden };

            var newStatus = request.Enabled ? UserStatuses.Active : UserStatuses.Disabled;
            await db.ExecuteAsync(
                "UPDATE ShopUser SET Status = @Status WHERE ShopId = @ShopId AND UserId = @UserId",
                new { request.ShopId, request.UserId, Status = newStatus });

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "user", request.UserId,
                request.Enabled ? "staff.enabled" : "staff.disabled", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
