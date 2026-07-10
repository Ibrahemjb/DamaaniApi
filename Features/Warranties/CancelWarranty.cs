using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// Cancel with mandatory reason — never delete (BP §10.13/§18). Owner-only at
// the controller; the record and its history stay, and the public page shows
// Cancelled from the same row immediately.
public class CancelWarranty
{
    public static class ReasonCodes
    {
        public const string SoldByMistake = "sold_by_mistake";
        public const string CustomerReturn = "customer_return";
        public const string FraudSuspicion = "fraud_suspicion";
        public const string Other = "other";

        public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        {
            SoldByMistake,
            CustomerReturn,
            FraudSuspicion,
            Other
        };
    }

    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? WarrantyId { get; set; }
        public string ReasonCode { get; set; } = "";
        public string? ReasonText { get; set; }
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
            RuleFor(x => x.WarrantyId).NotEmpty();
            RuleFor(x => x.ReasonCode).Must(x => ReasonCodes.Supported.Contains(x));
            RuleFor(x => x.ReasonText)
                .NotEmpty()
                .When(x => string.Equals(x.ReasonCode, ReasonCodes.Other, StringComparison.OrdinalIgnoreCase));
            RuleFor(x => x.ReasonText).MaximumLength(250);
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
                var status = await db.ExecuteScalarAsync<string?>(
                    "SELECT Status FROM Warranty WHERE Id = @WarrantyId AND ShopId = @ShopId",
                    new { request.WarrantyId, request.ShopId }, tx);

                if (status == null)
                    return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                if (status == WarrantyStatuses.Cancelled)
                    return new Result { Success = false, ErrorCode = ErrorCodes.WarrantyCancelled };

                var reasonText = string.IsNullOrWhiteSpace(request.ReasonText) ? null : request.ReasonText.Trim();
                var reason = reasonText == null ? request.ReasonCode : $"{request.ReasonCode}: {reasonText}";

                await db.ExecuteAsync(
                    """
                    UPDATE Warranty
                    SET Status = @Cancelled, CancelReason = @Reason, CancelledAt = UTC_TIMESTAMP(),
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @WarrantyId AND ShopId = @ShopId
                    """,
                    new
                    {
                        request.WarrantyId,
                        request.ShopId,
                        Cancelled = WarrantyStatuses.Cancelled,
                        Reason = reason
                    }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "warranty", request.WarrantyId!, "warranty.cancelled",
                    request.ActorUserId,
                    System.Text.Json.JsonSerializer.Serialize(new { reasonCode = request.ReasonCode, reasonText }));

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
