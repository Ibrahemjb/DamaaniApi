using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Warranties;
using DammaniAPI.Services.Storage;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using MySql.Data.MySqlClient;

namespace DammaniAPI.Features.Public;

// Public, unauthenticated service request submission (DMN-504, BP §10.16–10.17
// /§18). Server is authoritative on every rule the client also checks: field
// validation, file count/size/type (magic-byte sniff for images), expired/
// cancelled warranty rules, and a per-IP+slug rate limit. Attachments are
// stored via IFileStorage under a NON-public directory with random names.
// Field validation runs via MVC auto-validation on the bound command; file
// checks run in the handler because the controller maps IFormFile → payloads
// after binding (handlers never see HTTP types).
public class SubmitServiceRequest
{
    public class FilePayload
    {
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public byte[] Content { get; set; } = [];
    }

    public class Command : IRequest<Result>
    {
        public string? Slug { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string ProblemType { get; set; } = "";
        public string Description { get; set; } = "";
        public string? PreferredContact { get; set; }
        public bool Consent { get; set; }

        // Enriched by the controller — never bound from the form.
        public string? ClientIp { get; set; }
        public List<FilePayload> Files { get; set; } = new();
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? RequestNumber { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Slug).NotEmpty();
            RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.CustomerPhone)
                .NotEmpty()
                .Matches(@"^\+?[0-9\s\-()]{7,20}$")
                .WithMessage("A valid phone number is required so the shop can contact you.");
            RuleFor(x => x.ProblemType)
                .NotEmpty()
                .Must(x => ProblemTypes.Supported.Contains(x));
            RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
            RuleFor(x => x.PreferredContact)
                .Must(x => PreferredContacts.Supported.Contains(x!))
                .When(x => !string.IsNullOrWhiteSpace(x.PreferredContact));
            // BP §10.16: consent is a hard requirement, stored with timestamp.
            RuleFor(x => x.Consent).Equal(true);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        // File policy (DMN-504): up to 3 files, 5MB each, photos/videos only.
        internal const int MaxFiles = 3;
        internal const long MaxFileSizeBytes = 5 * 1024 * 1024;

        internal static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["image/jpeg"] = ".jpg",
                ["image/png"] = ".png",
                ["image/webp"] = ".webp",
                ["image/heic"] = ".heic",
                ["video/mp4"] = ".mp4",
                ["video/quicktime"] = ".mov"
            };

        // Abuse safety (BP spirit): 3 submissions per hour per IP+slug.
        internal const int RateLimitMax = 3;
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

        private readonly IManagementDatabase _mdb;
        private readonly IFileStorage _storage;
        private readonly IMemoryCache _cache;

        public CommandHandler(IManagementDatabase mdb, IFileStorage storage, IMemoryCache cache)
        {
            _mdb = mdb;
            _storage = storage;
            _cache = cache;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var rateKey = $"sr-rate:{request.ClientIp}:{request.Slug}";
            if (_cache.TryGetValue(rateKey, out int submitted) && submitted >= RateLimitMax)
                return new Result { Success = false, ErrorCode = ErrorCodes.TooManyRequests };

            if (!FilesAreValid(request.Files))
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidFiles };

            using var db = _mdb.Open();
            var warranty = await db.QueryFirstOrDefaultAsync<WarrantyRow>(
                """
                SELECT w.Id, w.ShopId,
                       CASE WHEN w.Status = @Active AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()
                            THEN 'expired' ELSE w.Status END AS Status,
                       s.Status AS ShopStatus
                FROM Warranty w
                JOIN Shop s ON s.Id = w.ShopId
                WHERE w.PublicSlug = @Slug
                """,
                new { Slug = request.Slug!.Trim(), Active = WarrantyStatuses.Active });

            // Same neutrality rules as GetPublicWarranty: drafts and unknown
            // slugs are indistinguishable; suspended shops are "unavailable".
            if (warranty == null || warranty.Status == WarrantyStatuses.Draft)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
            if (!string.Equals(warranty.ShopStatus, ShopStatuses.Active, StringComparison.OrdinalIgnoreCase))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unavailable };
            if (warranty.Status == WarrantyStatuses.Cancelled)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };
            // BP §18 expired rule. The shop setting arrives with DMN-901;
            // documented default until then: expired requests allowed.
            const bool allowExpiredRequests = true;
            if (warranty.Status == "expired" && !allowExpiredRequests)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };

            // File writes are not transactional; a rollback below can orphan
            // files on disk but never leaves DB rows pointing at nothing.
            var subdirectory = $"attachments/{DateTime.UtcNow:yyyyMM}";
            var stored = new List<(FilePayload File, string Path)>();
            foreach (var file in request.Files)
            {
                using var content = new MemoryStream(file.Content);
                stored.Add((file, await _storage.SaveAsync(
                    content, subdirectory, AllowedContentTypes[file.ContentType], ct)));
            }

            var requestId = Guid.NewGuid().ToString();
            string requestNumber;
            using var tx = db.BeginTransaction();
            try
            {
                requestNumber = await InsertWithGeneratedNumberAsync(db, tx, new
                {
                    Id = requestId,
                    warranty.ShopId,
                    WarrantyId = warranty.Id,
                    CustomerName = request.CustomerName.Trim(),
                    CustomerPhone = CreateWarranty.CommandHandler.NormalizePhone(request.CustomerPhone),
                    request.ProblemType,
                    Description = request.Description.Trim(),
                    PreferredContact = CreateWarranty.CommandHandler.NullIfBlank(request.PreferredContact),
                    Status = ServiceRequestStatuses.New,
                    Source = ServiceRequestSources.Public
                });

                foreach (var (file, path) in stored)
                    await db.ExecuteAsync(
                        """
                        INSERT INTO Attachment (Id, ShopId, ServiceRequestId, FilePath, OriginalName, ContentType, SizeBytes)
                        VALUES (@Id, @ShopId, @ServiceRequestId, @FilePath, @OriginalName, @ContentType, @SizeBytes)
                        """,
                        new
                        {
                            Id = Guid.NewGuid().ToString(),
                            warranty.ShopId,
                            ServiceRequestId = requestId,
                            FilePath = path,
                            OriginalName = Truncate(file.FileName, 200),
                            file.ContentType,
                            SizeBytes = file.Content.Length
                        }, tx);

                // ChangedByUserId NULL = customer action (DMN-503 rule).
                await db.ExecuteAsync(
                    """
                    INSERT INTO ServiceRequestStatusHistory (Id, ServiceRequestId, FromStatus, ToStatus, ChangedByUserId)
                    VALUES (@Id, @ServiceRequestId, NULL, @ToStatus, NULL)
                    """,
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        ServiceRequestId = requestId,
                        ToStatus = ServiceRequestStatuses.New
                    }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, warranty.ShopId, "request", requestId, "request.created",
                    actorUserId: null,
                    detailsJson: $$"""{"requestNumber":"{{requestNumber}}"}""");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            // Count successful submissions only — a validation trip-up must
            // not lock a legitimate customer out of the window.
            _cache.Set(rateKey, submitted + 1, RateLimitWindow);

            return new Result { Success = true, RequestNumber = requestNumber };
        }

        private static async Task<string> InsertWithGeneratedNumberAsync(
            System.Data.IDbConnection db, System.Data.IDbTransaction tx, object serviceRequest)
        {
            // Global SR-{yyMM}-{seq} sequence, same scheme as Warranty.Code
            // ("SR-2607-" is 8 chars → sequence starts at position 9).
            var monthPrefix = $"SR-{DateTime.UtcNow:yyMM}-";
            var nextSequence = await db.ExecuteScalarAsync<int?>(
                "SELECT MAX(CAST(SUBSTRING(RequestNumber, 9) AS UNSIGNED)) FROM ServiceRequest WHERE RequestNumber LIKE @Pattern",
                new { Pattern = monthPrefix + "%" }, tx) ?? 0;

            for (var attempt = 0; ; attempt++)
            {
                nextSequence++;
                var requestNumber = FormatRequestNumber(monthPrefix, nextSequence);
                var parameters = new DynamicParameters(serviceRequest);
                parameters.Add("RequestNumber", requestNumber);
                try
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO ServiceRequest
                            (Id, ShopId, WarrantyId, RequestNumber, CustomerName, CustomerPhone,
                             ProblemType, Description, PreferredContact, Status, Source, ConsentAt)
                        VALUES
                            (@Id, @ShopId, @WarrantyId, @RequestNumber, @CustomerName, @CustomerPhone,
                             @ProblemType, @Description, @PreferredContact, @Status, @Source, UTC_TIMESTAMP())
                        """,
                        parameters, tx);
                    return requestNumber;
                }
                catch (MySqlException ex) when (ex.Number == 1062 && attempt < 5)
                {
                    // Concurrent submission took this number; retry with next.
                }
            }
        }

        internal static string FormatRequestNumber(string monthPrefix, int sequence)
            => $"{monthPrefix}{sequence:0000}";

        internal static bool FilesAreValid(IReadOnlyList<FilePayload> files)
        {
            if (files.Count > MaxFiles)
                return false;
            foreach (var file in files)
            {
                if (file.Content.LongLength == 0 || file.Content.LongLength > MaxFileSizeBytes)
                    return false;
                if (!AllowedContentTypes.ContainsKey(file.ContentType))
                    return false;
                // Sniff magic bytes for images: a renamed .exe with an image
                // content type must be rejected. Videos pass on the type
                // whitelist (both allowed types are ISO-BMFF; older QuickTime
                // layouts vary too much for a safe strict check).
                if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    && !HasValidImageSignature(file.ContentType, file.Content))
                    return false;
            }
            return true;
        }

        internal static bool HasValidImageSignature(string contentType, byte[] content)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => StartsWith(content, [0xFF, 0xD8, 0xFF]),
                "image/png" => StartsWith(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                "image/webp" => content.Length >= 12
                    && StartsWith(content, "RIFF"u8.ToArray())
                    && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                // HEIC is ISO-BMFF: size (4 bytes) then "ftyp".
                "image/heic" => content.Length >= 8 && content.AsSpan(4, 4).SequenceEqual("ftyp"u8),
                _ => false
            };
        }

        private static bool StartsWith(byte[] content, byte[] prefix)
            => content.Length >= prefix.Length && content.AsSpan(0, prefix.Length).SequenceEqual(prefix);

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];

        private sealed class WarrantyRow
        {
            public string? Id { get; set; }
            public string? ShopId { get; set; }
            public string? Status { get; set; }
            public string? ShopStatus { get; set; }
        }
    }
}
