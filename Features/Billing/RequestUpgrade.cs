using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Billing;

// Manual-activation contract (DMN-1003): parks target in ScheduledPlanId, returns
// reference code. Admin applies via DMN-1103 changeShopPlan + Payment row.
public class RequestUpgrade
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
        public string? ReferenceCode { get; set; }
        public string? InstructionsEn { get; set; }
        public string? InstructionsAr { get; set; }
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
            if (target.SortOrder <= current.SortOrder)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };

            var reference = GenerateReferenceCode();
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
                    "subscription.upgrade_requested",
                    request.ActorUserId,
                    $"{{\"planCode\":\"{target.Code}\",\"reference\":\"{reference}\"}}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result
            {
                Success = true,
                ReferenceCode = reference,
                InstructionsEn =
                    $"Send payment via bank transfer, Jawwal Pay, or PalPay and quote reference {reference}. We activate within 24 hours.",
                InstructionsAr =
                    $"أرسل الدفع عبر تحويل بنكي أو Jawwal Pay أو PalPay مع ذكر الرمز {reference}. نفعّل خلال 24 ساعة."
            };
        }

        internal static string GenerateReferenceCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var suffix = new char[6];
            for (var i = 0; i < 6; i++)
                suffix[i] = chars[Random.Shared.Next(chars.Length)];
            return "UP-" + new string(suffix);
        }
    }
}
