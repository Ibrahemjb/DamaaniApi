using System.Text.Json;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// Edit mode + draft activation (DMN-407, BP §10.13 "forgiving"): corrections
// persist with an audit trail of old→new for the critical fields; activating a
// draft reruns create-level requirements including the monthly usage check.
// Cancelled warranties are never editable.
public class UpdateWarranty
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? WarrantyId { get; set; }

        // Customer (phone change re-links via upsert — same rule as create)
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string? CustomerCity { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerNotes { get; set; }

        // Product
        public string ProductName { get; set; } = "";
        public string? Category { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? ColorSpecs { get; set; }
        public string? PurchaseReference { get; set; }

        // Warranty period
        public DateTime? PurchaseDate { get; set; }
        public int? DurationMonths { get; set; }

        // Terms (snapshot text — editable per warranty)
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }

        // Draft → active (reruns required-field + usage checks)
        public bool Activate { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public CreateWarranty.UsageInfo? Usage { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.WarrantyId).NotEmpty();
            RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.CustomerPhone).NotEmpty().Matches(@"^\+?[0-9\s\-()]{7,20}$");
            RuleFor(x => x.ProductName).NotEmpty().MaximumLength(160);
            RuleFor(x => x.PurchaseDate).NotNull().When(x => x.Activate);
            RuleFor(x => x.DurationMonths).NotNull().When(x => x.Activate);
            RuleFor(x => x.DurationMonths).InclusiveBetween(1, 120).When(x => x.DurationMonths.HasValue);
            RuleFor(x => x.PurchaseDate)
                .LessThanOrEqualTo(_ => DateTime.UtcNow.Date.AddDays(30))
                .When(x => x.PurchaseDate.HasValue);
            RuleFor(x => x.Category)
                .Must(x => BusinessCategories.Supported.Contains(x!))
                .When(x => !string.IsNullOrWhiteSpace(x.Category));
            RuleFor(x => x.CustomerCity).MaximumLength(80);
            RuleFor(x => x.CustomerAddress).MaximumLength(255);
            RuleFor(x => x.CustomerNotes).MaximumLength(500);
            RuleFor(x => x.Model).MaximumLength(120);
            RuleFor(x => x.SerialNumber).MaximumLength(120);
            RuleFor(x => x.ColorSpecs).MaximumLength(160);
            RuleFor(x => x.PurchaseReference).MaximumLength(120);
        }
    }

    internal class CurrentRow
    {
        public string? Status { get; set; }
        public string? CustomerId { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public int? DurationMonths { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? CustomerPhone { get; set; }
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
                var current = await db.QueryFirstOrDefaultAsync<CurrentRow>(
                    """
                    SELECT w.Status, w.CustomerId, w.SerialNumber, w.PurchaseDate, w.DurationMonths, w.ExpiryDate,
                           c.Phone AS CustomerPhone
                    FROM Warranty w
                    JOIN Customer c ON c.Id = w.CustomerId
                    WHERE w.Id = @WarrantyId AND w.ShopId = @ShopId
                    """,
                    new { request.WarrantyId, request.ShopId }, tx);

                if (current == null)
                    return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                if (current.Status == WarrantyStatuses.Cancelled)
                    return new Result { Success = false, ErrorCode = ErrorCodes.WarrantyCancelled };

                var activating = request.Activate && current.Status == WarrantyStatuses.Draft;
                if (activating)
                {
                    // Activation consumes quota through the same gate as create.
                    var usage = await WarrantyUsage.GetForShopAsync(db, tx, request.ShopId);
                    if (usage.Blocked)
                        return new Result
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.PlanLimitReached,
                            Usage = new CreateWarranty.UsageInfo { Used = usage.Used, Limit = usage.Limit }
                        };
                }

                // Customer: same phone updates the row; a corrected phone
                // re-links through the create upsert (old row is kept).
                var customerId = current.CustomerId!;
                var normalizedPhone = CreateWarranty.CommandHandler.NormalizePhone(request.CustomerPhone);
                if (normalizedPhone != current.CustomerPhone)
                {
                    customerId = await UpsertByPhoneAsync(db, tx, request, normalizedPhone);
                }
                else
                {
                    await db.ExecuteAsync(
                        """
                        UPDATE Customer
                        SET Name = @Name, City = @City, Address = @Address, Notes = @Notes, UpdatedAt = UTC_TIMESTAMP()
                        WHERE Id = @Id
                        """,
                        new
                        {
                            Id = customerId,
                            Name = request.CustomerName.Trim(),
                            City = NullIfBlank(request.CustomerCity),
                            Address = NullIfBlank(request.CustomerAddress),
                            Notes = NullIfBlank(request.CustomerNotes)
                        }, tx);
                }

                // Expiry is always recomputed server-side (never trusted from
                // the client) whenever date/duration are present.
                DateTime? expiryDate = request.PurchaseDate.HasValue && request.DurationMonths.HasValue
                    ? request.PurchaseDate.Value.Date.AddMonths(request.DurationMonths.Value)
                    : null;

                var newSerial = NullIfBlank(request.SerialNumber);
                var changes = BuildChangeLog(current, request, newSerial, expiryDate, activating);

                await db.ExecuteAsync(
                    """
                    UPDATE Warranty
                    SET CustomerId = @CustomerId, ProductName = @ProductName, Category = @Category,
                        Model = @Model, SerialNumber = @SerialNumber, ColorSpecs = @ColorSpecs,
                        PurchaseReference = @PurchaseReference, PurchaseDate = @PurchaseDate,
                        DurationMonths = @DurationMonths, ExpiryDate = @ExpiryDate,
                        TermsAr = @TermsAr, TermsEn = @TermsEn, Status = @Status,
                        UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @WarrantyId AND ShopId = @ShopId
                    """,
                    new
                    {
                        request.WarrantyId,
                        request.ShopId,
                        CustomerId = customerId,
                        ProductName = request.ProductName.Trim(),
                        Category = NullIfBlank(request.Category),
                        Model = NullIfBlank(request.Model),
                        SerialNumber = newSerial,
                        ColorSpecs = NullIfBlank(request.ColorSpecs),
                        PurchaseReference = NullIfBlank(request.PurchaseReference),
                        PurchaseDate = request.PurchaseDate?.Date,
                        request.DurationMonths,
                        ExpiryDate = expiryDate,
                        TermsAr = NullIfBlank(request.TermsAr),
                        TermsEn = NullIfBlank(request.TermsEn),
                        Status = activating ? WarrantyStatuses.Active : current.Status
                    }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "warranty", request.WarrantyId!,
                    activating ? "warranty.activated" : "warranty.updated",
                    request.ActorUserId,
                    changes);

                tx.Commit();
                return new Result { Success = true };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static async Task<string> UpsertByPhoneAsync(
            System.Data.IDbConnection db, System.Data.IDbTransaction tx, Command request, string normalizedPhone)
        {
            var existing = await db.ExecuteScalarAsync<string?>(
                "SELECT Id FROM Customer WHERE ShopId = @ShopId AND Phone = @Phone",
                new { request.ShopId, Phone = normalizedPhone }, tx);
            if (existing != null)
            {
                await db.ExecuteAsync(
                    "UPDATE Customer SET Name = @Name, City = @City, UpdatedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                    new { Id = existing, Name = request.CustomerName.Trim(), City = NullIfBlank(request.CustomerCity) }, tx);
                return existing;
            }

            var customerId = Guid.NewGuid().ToString();
            await db.ExecuteAsync(
                """
                INSERT INTO Customer (Id, ShopId, Name, Phone, City, Address, Notes)
                VALUES (@Id, @ShopId, @Name, @Phone, @City, @Address, @Notes)
                """,
                new
                {
                    Id = customerId,
                    request.ShopId,
                    Name = request.CustomerName.Trim(),
                    Phone = normalizedPhone,
                    City = NullIfBlank(request.CustomerCity),
                    Address = NullIfBlank(request.CustomerAddress),
                    Notes = NullIfBlank(request.CustomerNotes)
                }, tx);
            return customerId;
        }

        // Old→new audit for the critical verification fields (BP §14); the
        // timeline renders these in the detail page.
        internal static string BuildChangeLog(
            CurrentRow current, Command request, string? newSerial, DateTime? newExpiry, bool activating)
        {
            var changes = new Dictionary<string, object?>();
            if (current.SerialNumber != newSerial)
                changes["serialNumber"] = new { old = current.SerialNumber, @new = newSerial };
            if (current.PurchaseDate != request.PurchaseDate?.Date)
                changes["purchaseDate"] = new { old = Iso(current.PurchaseDate), @new = Iso(request.PurchaseDate?.Date) };
            if (current.DurationMonths != request.DurationMonths)
                changes["durationMonths"] = new { old = current.DurationMonths, @new = request.DurationMonths };
            if (current.ExpiryDate != newExpiry)
                changes["expiryDate"] = new { old = Iso(current.ExpiryDate), @new = Iso(newExpiry) };
            if (activating)
                changes["status"] = new { old = WarrantyStatuses.Draft, @new = WarrantyStatuses.Active };
            return JsonSerializer.Serialize(changes);
        }

        private static string? Iso(DateTime? date) => date?.ToString("yyyy-MM-dd");

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
