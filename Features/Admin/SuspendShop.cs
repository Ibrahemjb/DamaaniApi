using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class SuspendShop
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string ShopId { get; set; } = "";
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
            RuleFor(x => x.ShopId).NotEmpty();
            RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var exists = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Shop WHERE Id = @ShopId",
                new { request.ShopId });
            if (exists == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Shop
                    SET Status = @Suspended, SuspensionNote = @Note, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @ShopId
                    """,
                    new { request.ShopId, Note = request.Note.Trim(), Suspended = ShopStatuses.Suspended },
                    tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "shop", request.ShopId, "shop.suspended",
                    request.ActorUserId, $"{{\"note\":\"{EscapeJson(request.Note.Trim())}\"}}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true };
        }

        internal static string EscapeJson(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
