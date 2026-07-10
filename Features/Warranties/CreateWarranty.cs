using System.Security.Cryptography;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;
using MySql.Data.MySqlClient;

namespace DammaniAPI.Features.Warranties;

// The core create/draft slice (DMN-402, BP §10.9/§13/§18): customer upsert by
// phone, template terms snapshot, server-computed expiry, DM-YYMM-NNNN code +
// random public slug, duplicate-serial warning flow, and monthly plan limit
// enforcement (non-drafts only).
public class CreateWarranty
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }

        // Customer
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

        // Warranty
        public DateTime? PurchaseDate { get; set; }
        public int? DurationMonths { get; set; }
        public string? TemplateId { get; set; }
        public string? BranchId { get; set; }

        // Terms (edited text from the form; falls back to template snapshot)
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }

        public bool IsDraft { get; set; }
        public bool AcknowledgeDuplicate { get; set; }
    }

    public class UsageInfo
    {
        public int Used { get; set; }
        public int Limit { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? WarrantyId { get; set; }
        public string? Code { get; set; }
        public string? PublicSlug { get; set; }
        public string? PublicUrl { get; set; }
        public UsageInfo? Usage { get; set; }
        public string? ExistingWarrantyCode { get; set; }
        public string? ExistingProductName { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            // Name + phone + product are the draft minimum (Customer row needs
            // them; BP: "Save Draft available only with enough information").
            RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.CustomerPhone)
                .NotEmpty()
                .Matches(@"^\+?[0-9\s\-()]{7,20}$")
                .WithMessage("Phone number is required to find this warranty later.");
            RuleFor(x => x.ProductName).NotEmpty().MaximumLength(160);

            RuleFor(x => x.PurchaseDate).NotNull().When(x => !x.IsDraft);
            RuleFor(x => x.DurationMonths).NotNull().When(x => !x.IsDraft);
            RuleFor(x => x.DurationMonths).InclusiveBetween(1, 120).When(x => x.DurationMonths.HasValue);
            // Sanity, not strictness: same-day and slightly future-dated sales
            // are legitimate; more than 30 days ahead is a data-entry mistake.
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

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IConfiguration _configuration;

        public CommandHandler(IManagementDatabase mdb, IConfiguration configuration)
        {
            _mdb = mdb;
            _configuration = configuration;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ActorUserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var shopStatus = await db.ExecuteScalarAsync<string?>(
                    "SELECT Status FROM Shop WHERE Id = @ShopId", new { request.ShopId }, tx);
                if (shopStatus == null)
                    return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };
                if (shopStatus == ShopStatuses.Suspended)
                    return new Result { Success = false, ErrorCode = ErrorCodes.ShopSuspended };

                // 1. Monthly plan limit — new non-draft cards only (BP §13).
                if (!request.IsDraft)
                {
                    var usage = await WarrantyUsage.GetForShopAsync(db, tx, request.ShopId);
                    if (usage.Blocked)
                        return new Result
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.PlanLimitReached,
                            Usage = new UsageInfo { Used = usage.Used, Limit = usage.Limit }
                        };
                }

                // 2. Duplicate serial warns until acknowledged; never blocks after.
                var serial = NullIfBlank(request.SerialNumber);
                if (serial != null && !request.AcknowledgeDuplicate)
                {
                    var existing = await CheckSerialDuplicate.FindAsync(db, tx, request.ShopId, serial);
                    if (existing != null)
                        return new Result
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.DuplicateSerial,
                            ExistingWarrantyCode = existing.Code,
                            ExistingProductName = existing.ProductName
                        };
                }

                // 3. Terms snapshot: edited text wins; otherwise copy the
                //    template's terms NOW — never referenced live again (BP §18).
                string? termsAr = NullIfBlank(request.TermsAr), termsEn = NullIfBlank(request.TermsEn);
                if (!string.IsNullOrWhiteSpace(request.TemplateId))
                {
                    var template = await db.QueryFirstOrDefaultAsync<(string? TermsAr, string? TermsEn)?>(
                        "SELECT TermsAr, TermsEn FROM WarrantyTemplate WHERE Id = @TemplateId AND ShopId = @ShopId",
                        new { request.TemplateId, request.ShopId }, tx);
                    if (template == null)
                        return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                    termsAr ??= NullIfBlank(template.Value.TermsAr);
                    termsEn ??= NullIfBlank(template.Value.TermsEn);
                }

                // 4. Customer upsert by (ShopId, normalized phone).
                var customerId = await UpsertCustomerAsync(db, tx, request);

                // 5. Server-computed expiry is authoritative.
                DateTime? expiryDate = request.PurchaseDate.HasValue && request.DurationMonths.HasValue
                    ? request.PurchaseDate.Value.Date.AddMonths(request.DurationMonths.Value)
                    : null;

                // 6/7. Insert with generated code + slug (retry on rare collisions).
                var warrantyId = Guid.NewGuid().ToString();
                var status = request.IsDraft ? WarrantyStatuses.Draft : WarrantyStatuses.Active;
                var (code, slug) = await InsertWithGeneratedCodesAsync(db, tx, new
                {
                    Id = warrantyId,
                    request.ShopId,
                    CustomerId = customerId,
                    request.BranchId,
                    TemplateId = NullIfBlank(request.TemplateId),
                    ProductName = request.ProductName.Trim(),
                    Category = NullIfBlank(request.Category),
                    Model = NullIfBlank(request.Model),
                    SerialNumber = serial,
                    ColorSpecs = NullIfBlank(request.ColorSpecs),
                    PurchaseReference = NullIfBlank(request.PurchaseReference),
                    PurchaseDate = request.PurchaseDate?.Date,
                    request.DurationMonths,
                    ExpiryDate = expiryDate,
                    TermsAr = termsAr,
                    TermsEn = termsEn,
                    Status = status,
                    CreatedByUserId = request.ActorUserId
                });

                if (!string.IsNullOrWhiteSpace(request.TemplateId))
                    await db.ExecuteAsync(
                        "UPDATE WarrantyTemplate SET LastUsedAt = UTC_TIMESTAMP() WHERE Id = @TemplateId AND ShopId = @ShopId",
                        new { request.TemplateId, request.ShopId }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "warranty", warrantyId,
                    request.IsDraft ? "warranty.draft_saved" : "warranty.created",
                    request.ActorUserId);

                tx.Commit();

                return new Result
                {
                    Success = true,
                    WarrantyId = warrantyId,
                    Code = code,
                    PublicSlug = slug,
                    PublicUrl = BuildPublicUrl(_configuration, slug)
                };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static async Task<string> UpsertCustomerAsync(
            System.Data.IDbConnection db, System.Data.IDbTransaction tx, Command request)
        {
            var phone = NormalizePhone(request.CustomerPhone);
            var name = request.CustomerName.Trim();
            var city = NullIfBlank(request.CustomerCity);

            var existing = await db.QueryFirstOrDefaultAsync<(string Id, string Name, string? City)?>(
                "SELECT Id, Name, City FROM Customer WHERE ShopId = @ShopId AND Phone = @Phone",
                new { request.ShopId, Phone = phone }, tx);

            if (existing != null)
            {
                // Same phone reuses the customer row; refresh name/city when
                // changed (DMN-402 spec — other fields stay owner-managed).
                if (existing.Value.Name != name || existing.Value.City != city)
                    await db.ExecuteAsync(
                        "UPDATE Customer SET Name = @Name, City = @City, UpdatedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                        new { existing.Value.Id, Name = name, City = city }, tx);
                return existing.Value.Id;
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
                    Name = name,
                    Phone = phone,
                    City = city,
                    Address = NullIfBlank(request.CustomerAddress),
                    Notes = NullIfBlank(request.CustomerNotes)
                }, tx);
            return customerId;
        }

        private static async Task<(string Code, string Slug)> InsertWithGeneratedCodesAsync(
            System.Data.IDbConnection db, System.Data.IDbTransaction tx, object warranty)
        {
            var monthPrefix = $"DM-{DateTime.UtcNow:yyMM}-";
            var nextSequence = await db.ExecuteScalarAsync<int?>(
                "SELECT MAX(CAST(SUBSTRING(Code, 9) AS UNSIGNED)) FROM Warranty WHERE Code LIKE @Pattern",
                new { Pattern = monthPrefix + "%" }, tx) ?? 0;

            for (var attempt = 0; ; attempt++)
            {
                nextSequence++;
                var code = FormatCode(monthPrefix, nextSequence);
                var slug = GeneratePublicSlug();
                var parameters = new DynamicParameters(warranty);
                parameters.Add("Code", code);
                parameters.Add("PublicSlug", slug);
                try
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO Warranty
                            (Id, ShopId, CustomerId, BranchId, TemplateId, Code, PublicSlug,
                             ProductName, Category, Model, SerialNumber, ColorSpecs, PurchaseReference,
                             PurchaseDate, DurationMonths, ExpiryDate, TermsAr, TermsEn, Status, CreatedByUserId)
                        VALUES
                            (@Id, @ShopId, @CustomerId, @BranchId, @TemplateId, @Code, @PublicSlug,
                             @ProductName, @Category, @Model, @SerialNumber, @ColorSpecs, @PurchaseReference,
                             @PurchaseDate, @DurationMonths, @ExpiryDate, @TermsAr, @TermsEn, @Status, @CreatedByUserId)
                        """,
                        parameters, tx);
                    return (code, slug);
                }
                catch (MySqlException ex) when (ex.Number == 1062 && attempt < 5)
                {
                    // Concurrent create took this code (or, vanishingly, the
                    // slug); bump the sequence and retry with fresh values.
                }
            }
        }

        internal static string FormatCode(string monthPrefix, int sequence)
            => $"{monthPrefix}{sequence:0000}";

        // URL-safe, non-guessable public identifier (~103 bits of entropy);
        // the only warranty identifier ever shared outside the shop (DMN-401).
        internal static string GeneratePublicSlug()
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
            var bytes = RandomNumberGenerator.GetBytes(20);
            return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
        }

        // Digits with optional leading + — so the same phone typed with spaces
        // or dashes always maps to the same Customer row.
        internal static string NormalizePhone(string phone)
        {
            var trimmed = phone.Trim();
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            return trimmed.StartsWith('+') ? "+" + digits : digits;
        }

        internal static string BuildPublicUrl(IConfiguration configuration, string slug)
            => $"{(configuration["APP_BASE_URL"] ?? "http://localhost:5173").TrimEnd('/')}/w/{slug}";

        internal static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
