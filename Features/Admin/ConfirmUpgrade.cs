using System.Data;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class ConfirmUpgrade
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string ShopId { get; set; } = "";
        public string? Reference { get; set; }
        public decimal? AmountUsdOverride { get; set; }
        public decimal? AmountIlsOverride { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? PlanCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.ShopId).NotEmpty();
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
            var target = await PlanQueries.GetByIdAsync(db, null, sub.ScheduledPlanId);
            if (current is null || target is null || !AdminPlanRules.IsUpgradeRequest(current, target))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };

            var (periodStart, periodEnd) = SubscriptionRoller.PeriodForDate(DateTime.UtcNow);
            var amountUsd = request.AmountUsdOverride ?? target.PriceUsd;
            var amountIls = request.AmountIlsOverride ?? target.PriceIls;
            var reference = string.IsNullOrWhiteSpace(request.Reference)
                ? GetPlanOverview.QueryHandler.ExtractReference(await LoadLastUpgradeDetailsAsync(db, request.ShopId))
                : request.Reference.Trim();

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

                var paymentId = Guid.NewGuid().ToString();
                await db.ExecuteAsync(
                    """
                    INSERT INTO Payment
                        (Id, ShopId, SubscriptionId, PlanId, AmountUsd, AmountIls, Method, Status, Reference, PaidAt, CreatedAt)
                    VALUES
                        (@Id, @ShopId, @SubscriptionId, @PlanId, @AmountUsd, @AmountIls, 'manual', 'paid', @Reference, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                    """,
                    new
                    {
                        Id = paymentId,
                        request.ShopId,
                        SubscriptionId = sub.Id,
                        PlanId = target.Id,
                        AmountUsd = amountUsd,
                        AmountIls = amountIls,
                        Reference = reference
                    },
                    tx);

                var details = $"{{\"planCode\":\"{target.Code}\",\"reference\":\"{reference ?? ""}\"";
                if (request.AmountUsdOverride.HasValue || request.AmountIlsOverride.HasValue)
                    details += ",\"amountOverride\":true";
                details += "}";

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "subscription", sub.Id, "plan.upgrade_confirmed",
                    request.ActorUserId, details);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, PlanCode = target.Code };
        }

        private static async Task<string?> LoadLastUpgradeDetailsAsync(IDbConnection db, string shopId)
            => await db.ExecuteScalarAsync<string?>(
                """
                SELECT Details FROM ActivityLog
                WHERE ShopId = @ShopId AND Action = 'subscription.upgrade_requested'
                ORDER BY CreatedAt DESC LIMIT 1
                """,
                new { ShopId = shopId });
    }
}
