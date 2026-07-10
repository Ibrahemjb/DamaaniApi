using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class RevokeInvite
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string InviteId { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.InviteId).NotEmpty();
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
            var deleted = await db.ExecuteAsync(
                """
                DELETE FROM StaffInvite
                WHERE Id = @InviteId AND ShopId = @ShopId AND AcceptedAt IS NULL
                """,
                new { request.InviteId, request.ShopId });

            if (deleted == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "staff_invite", request.InviteId, "staff.invite_revoked", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
