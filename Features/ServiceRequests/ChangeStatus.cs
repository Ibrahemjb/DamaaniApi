using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Change workflow status with optional note and notify flag (DMN-602).
// Closed requests are immutable — use CloseRequest to finish a case.
public class ChangeStatus
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? RequestId { get; set; }
        public string ToStatus { get; set; } = "";
        public string? Note { get; set; }
        public bool NotifiedCustomer { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    internal static readonly HashSet<string> AllowedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ServiceRequestStatuses.New,
        ServiceRequestStatuses.Reviewing,
        ServiceRequestStatuses.WaitingCustomer,
        ServiceRequestStatuses.SentSupplier,
        ServiceRequestStatuses.Repaired,
        ServiceRequestStatuses.Replaced,
        ServiceRequestStatuses.Rejected
    };

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.RequestId).NotEmpty();
            RuleFor(x => x.ToStatus).Must(x => AllowedTargets.Contains(x));
            RuleFor(x => x.Note).MaximumLength(500);
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

                var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                var fromStatus = row.Status;

                await db.ExecuteAsync(
                    """
                    UPDATE ServiceRequest
                    SET Status = @ToStatus, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @RequestId AND ShopId = @ShopId
                    """,
                    new { request.RequestId, request.ShopId, request.ToStatus }, tx);

                await db.ExecuteAsync(
                    """
                    INSERT INTO ServiceRequestStatusHistory
                        (Id, ServiceRequestId, FromStatus, ToStatus, Note, NotifiedCustomer, ChangedByUserId)
                    VALUES (@Id, @RequestId, @FromStatus, @ToStatus, @Note, @NotifiedCustomer, @ActorUserId)
                    """,
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        request.RequestId,
                        FromStatus = fromStatus,
                        request.ToStatus,
                        Note = note,
                        request.NotifiedCustomer,
                        request.ActorUserId
                    }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "request", request.RequestId!, "request.status_changed",
                    request.ActorUserId,
                    System.Text.Json.JsonSerializer.Serialize(new { from = fromStatus, to = request.ToStatus }));

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
