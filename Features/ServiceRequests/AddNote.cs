using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Internal team notes on a service request (DMN-602). Never shown publicly.
public class AddNote
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? RequestId { get; set; }
        public string Note { get; set; } = "";
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
            RuleFor(x => x.Note).NotEmpty().MinimumLength(1).MaximumLength(2000);
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

                var noteId = Guid.NewGuid().ToString();
                await db.ExecuteAsync(
                    """
                    INSERT INTO ServiceRequestNote (Id, ServiceRequestId, AuthorUserId, Note)
                    VALUES (@Id, @RequestId, @ActorUserId, @Note)
                    """,
                    new
                    {
                        Id = noteId,
                        request.RequestId,
                        request.ActorUserId,
                        Note = request.Note.Trim()
                    }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "request", request.RequestId!, "request.note_added",
                    request.ActorUserId);

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
