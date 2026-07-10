using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class RevertScheduledChange
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
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
            var sub = await SubscriptionRoller.LoadAsync(db, null, request.ShopId);
            if (sub is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Subscription
                    SET ScheduledPlanId = NULL,
                        CancelAtPeriodEnd = 0,
                        CancelReason = NULL,
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new { sub.Id },
                    tx);

                await ActivityLogger.LogAsync(
                    db,
                    tx,
                    request.ShopId,
                    "subscription",
                    sub.Id,
                    "subscription.change_reverted",
                    request.ActorUserId);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true };
        }
    }
}
