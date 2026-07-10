using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Assign a case to shop staff (DMN-602). Null clears assignment.
public class Assign
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? RequestId { get; set; }
        public string? AssignedToUserId { get; set; }
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
            RuleFor(x => x.RequestId).NotEmpty();
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ActorUserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var row = await ServiceRequestAccess.LoadAsync(db, tx, request.ShopId, request.RequestId!);
                if (row == null)
                    return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                if (ServiceRequestAccess.IsClosed(row.Status))
                    return new Result { Success = false, ErrorCode = ErrorCodes.RequestClosed };

                if (!string.IsNullOrWhiteSpace(request.AssignedToUserId))
                {
                    var isActiveStaff = await db.ExecuteScalarAsync<int>(
                        """
                        SELECT COUNT(*) FROM ShopUser
                        WHERE ShopId = @ShopId AND UserId = @UserId AND Status = @Active
                        """,
                        new
                        {
                            request.ShopId,
                            UserId = request.AssignedToUserId,
                            Active = UserStatuses.Active
                        }, tx);
                    if (isActiveStaff == 0)
                        return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                }

                await db.ExecuteAsync(
                    """
                    UPDATE ServiceRequest
                    SET AssignedToUserId = @AssignedToUserId, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @RequestId AND ShopId = @ShopId
                    """,
                    new { request.RequestId, request.ShopId, request.AssignedToUserId }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "request", request.RequestId!, "request.assigned",
                    request.ActorUserId,
                    System.Text.Json.JsonSerializer.Serialize(new { assignedToUserId = request.AssignedToUserId }));

                tx.Commit();
                return new Result { Success = true };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
