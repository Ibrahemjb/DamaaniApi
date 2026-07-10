using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Billing;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GrantExtension
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string ShopId { get; set; } = "";
        public int Days { get; set; }
        public string Note { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime? NewPeriodEnd { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.ShopId).NotEmpty();
            RuleFor(x => x.Days).InclusiveBetween(1, 90);
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

            var newEnd = sub.CurrentPeriodEnd.AddDays(request.Days);

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    UPDATE Subscription
                    SET CurrentPeriodEnd = @CurrentPeriodEnd, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new { CurrentPeriodEnd = newEnd, sub.Id },
                    tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "subscription", sub.Id, "plan.extension_granted",
                    request.ActorUserId,
                    $"{{\"days\":{request.Days},\"note\":\"{SuspendShop.CommandHandler.EscapeJson(request.Note.Trim())}\"}}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, NewPeriodEnd = newEnd };
        }
    }
}
