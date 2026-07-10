using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Billing;

public class CancelSubscription
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string ReasonCode { get; set; } = "";
        public string? Note { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime? EffectiveDate { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        {
            "too_expensive",
            "not_using",
            "missing_feature",
            "other"
        };

        public CommandValidator()
        {
            RuleFor(x => x.ReasonCode).Must(x => Supported.Contains(x));
            RuleFor(x => x.Note).MaximumLength(300);
        }
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

            var reason = BuildCancelReason(request.ReasonCode, request.Note);

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Subscription
                    SET CancelAtPeriodEnd = 1,
                        CancelReason = @CancelReason,
                        ScheduledPlanId = NULL,
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new { CancelReason = reason, sub.Id },
                    tx);

                await ActivityLogger.LogAsync(
                    db,
                    tx,
                    request.ShopId,
                    "subscription",
                    sub.Id,
                    "subscription.cancel_scheduled",
                    request.ActorUserId,
                    $"{{\"reasonCode\":\"{request.ReasonCode}\"}}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, EffectiveDate = sub.CurrentPeriodEnd };
        }

        internal static string BuildCancelReason(string code, string? note)
        {
            var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            return trimmed is null ? code : $"{code}: {trimmed}";
        }
    }
}
