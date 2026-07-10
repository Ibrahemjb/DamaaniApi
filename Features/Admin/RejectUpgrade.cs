using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class RejectUpgrade
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
            var sub = await SubscriptionRoller.LoadAsync(db, null, request.ShopId);
            if (sub is null || string.IsNullOrWhiteSpace(sub.ScheduledPlanId))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var current = await PlanQueries.GetByIdAsync(db, null, sub.PlanId);
            var scheduled = await PlanQueries.GetByIdAsync(db, null, sub.ScheduledPlanId);
            if (!AdminPlanRules.IsUpgradeRequest(current, scheduled))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Subscription
                    SET ScheduledPlanId = NULL, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new { sub.Id },
                    tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "subscription", sub.Id, "plan.upgrade_rejected",
                    request.ActorUserId,
                    $"{{\"note\":\"{SuspendShop.CommandHandler.EscapeJson(request.Note.Trim())}\"}}");

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
