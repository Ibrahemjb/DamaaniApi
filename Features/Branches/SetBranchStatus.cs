using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Branches;

public class SetBranchStatus
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string BranchId { get; set; } = "";
        public bool Active { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.BranchId).NotEmpty();
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var status = request.Active ? BranchStatuses.Active : BranchStatuses.Inactive;
            using var db = _mdb.Open();
            var updated = await db.ExecuteAsync(
                """
                UPDATE Branch SET Status = @Status, UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @BranchId AND ShopId = @ShopId
                """,
                new { request.BranchId, request.ShopId, Status = status });

            if (updated == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "branch", request.BranchId,
                request.Active ? "branch.activated" : "branch.deactivated", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
