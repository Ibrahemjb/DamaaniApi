using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class ScheduleDowngrade
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string PlanCode { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime? EffectiveDate { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(20);
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
            await SubscriptionRoller.RollIfNeededAsync(db, null, request.ShopId, DateTime.UtcNow);

            var sub = await SubscriptionRoller.LoadAsync(db, null, request.ShopId);
            if (sub is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var current = await PlanQueries.GetByIdAsync(db, null, sub.PlanId);
            var target = await PlanQueries.GetByCodeAsync(db, null, request.PlanCode.Trim().ToLowerInvariant());
            if (current is null || target is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
            if (target.SortOrder >= current.SortOrder)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Subscription
                    SET ScheduledPlanId = @ScheduledPlanId,
                        CancelAtPeriodEnd = 0,
                        CancelReason = NULL,
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new { ScheduledPlanId = target.Id, sub.Id },
                    tx);

                await ActivityLogger.LogAsync(
                    db,
                    tx,
                    request.ShopId,
                    "subscription",
                    sub.Id,
                    "subscription.downgrade_scheduled",
                    request.ActorUserId,
                    $"{{\"planCode\":\"{target.Code}\"}}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, EffectiveDate = sub.CurrentPeriodEnd };
        }
    }
}
