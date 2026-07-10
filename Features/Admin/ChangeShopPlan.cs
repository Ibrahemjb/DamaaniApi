using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class ChangeShopPlan
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string ShopId { get; set; } = "";
        public string PlanCode { get; set; } = "";
        public string Note { get; set; } = "";
        public bool RecordPayment { get; set; }
        public string? Reference { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? PlanCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.ShopId).NotEmpty();
            RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(20);
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
            if (sub is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var target = await PlanQueries.GetByCodeAsync(db, null, request.PlanCode.Trim().ToLowerInvariant());
            if (target is null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var (periodStart, periodEnd) = SubscriptionRoller.PeriodForDate(DateTime.UtcNow);

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Subscription
                    SET PlanId = @PlanId,
                        ScheduledPlanId = NULL,
                        CancelAtPeriodEnd = 0,
                        CancelReason = NULL,
                        CurrentPeriodStart = @CurrentPeriodStart,
                        CurrentPeriodEnd = @CurrentPeriodEnd,
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new
                    {
                        PlanId = target.Id,
                        CurrentPeriodStart = periodStart,
                        CurrentPeriodEnd = periodEnd,
                        sub.Id
                    },
                    tx);

                if (request.RecordPayment && target.PriceUsd > 0)
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO Payment
                            (Id, ShopId, SubscriptionId, PlanId, AmountUsd, AmountIls, Method, Status, Reference, PaidAt, CreatedAt)
                        VALUES
                            (@Id, @ShopId, @SubscriptionId, @PlanId, @AmountUsd, @AmountIls, 'manual', 'paid', @Reference, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                        """,
                        new
                        {
                            Id = Guid.NewGuid().ToString(),
                            request.ShopId,
                            SubscriptionId = sub.Id,
                            PlanId = target.Id,
                            AmountUsd = target.PriceUsd,
                            AmountIls = target.PriceIls,
                            Reference = request.Reference
                        },
                        tx);
                }

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "subscription", sub.Id, "plan.changed_manually",
                    request.ActorUserId,
                    $"{{\"planCode\":\"{target.Code}\",\"note\":\"{SuspendShop.CommandHandler.EscapeJson(request.Note.Trim())}\"}}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, PlanCode = target.Code };
        }
    }
}
