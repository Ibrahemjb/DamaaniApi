using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Public;

// Public warranty verification (DMN-501, BP §10.15/§11/§14). Unauthenticated:
// every Result field is an explicit whitelist pinned by contract test — no
// customer identity, no internal fields (CancelReason stays internal; the
// public page shows the cancelled STATUS only). Unknown slugs, drafts, and
// suspended shops return neutral errorCodes with no other data, so probing a
// slug reveals nothing. Responses are never cached: a cancellation must
// reflect on the next scan.
public class GetPublicWarranty
{
    public class Query : IRequest<Result>
    {
        public string? Slug { get; set; }
    }

    public class ShopInfo
    {
        public string? Name { get; set; }
        public string? LogoUrl { get; set; }
        public string? City { get; set; }
        public string? WhatsAppNumber { get; set; }
        public bool ShowAddress { get; set; }
        public bool ShowWhatsApp { get; set; }
        public string? PublicLanguage { get; set; }
        public bool AllowExpiredRequests { get; set; }
        public bool DamaaniBranding { get; set; }
    }

    public class WarrantyInfo
    {
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Status { get; set; }
        public string? TermsAr { get; set; }
        public string? TermsEn { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public ShopInfo? Shop { get; set; }
        public WarrantyInfo? Warranty { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Slug))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            using var db = _mdb.Open();
            var row = await db.QueryFirstOrDefaultAsync<Row>(
                """
                SELECT w.Code, w.ProductName, w.Model, w.SerialNumber,
                       w.PurchaseDate, w.ExpiryDate,
                       CASE WHEN w.Status = @Active AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()
                            THEN 'expired' ELSE w.Status END AS Status,
                       w.TermsAr, w.TermsEn,
                       s.Name AS ShopName, s.LogoPath AS ShopLogoPath, s.City AS ShopCity,
                       s.WhatsAppNumber AS ShopWhatsAppNumber, s.Status AS ShopStatus,
                       p.ShowDamaaniBranding
                FROM Warranty w
                JOIN Shop s ON s.Id = w.ShopId
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                WHERE w.PublicSlug = @Slug AND w.Status <> @Draft
                """,
                new
                {
                    Slug = request.Slug.Trim(),
                    Active = WarrantyStatuses.Active,
                    Draft = WarrantyStatuses.Draft
                });

            if (row == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            if (!string.Equals(row.ShopStatus, ShopStatuses.Active, StringComparison.OrdinalIgnoreCase))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unavailable };

            return new Result
            {
                Success = true,
                Shop = new ShopInfo
                {
                    Name = row.ShopName,
                    LogoUrl = row.ShopLogoPath,
                    City = row.ShopCity,
                    WhatsAppNumber = row.ShopWhatsAppNumber,
                    // Public-page settings arrive with DMN-901; until those
                    // columns exist the documented defaults apply: show all,
                    // Arabic default, expired requests allowed.
                    ShowAddress = true,
                    ShowWhatsApp = true,
                    PublicLanguage = Languages.Arabic,
                    AllowExpiredRequests = true,
                    // Missing subscription row (shouldn't happen — DMN-1001
                    // backfills) falls back to Free-plan behavior: branded.
                    DamaaniBranding = row.ShowDamaaniBranding ?? true
                },
                Warranty = new WarrantyInfo
                {
                    Code = row.Code,
                    ProductName = row.ProductName,
                    Model = row.Model,
                    SerialNumber = row.SerialNumber,
                    PurchaseDate = row.PurchaseDate,
                    ExpiryDate = row.ExpiryDate,
                    Status = row.Status,
                    TermsAr = row.TermsAr,
                    TermsEn = row.TermsEn
                }
            };
        }

        private sealed class Row
        {
            public string? Code { get; set; }
            public string? ProductName { get; set; }
            public string? Model { get; set; }
            public string? SerialNumber { get; set; }
            public DateTime? PurchaseDate { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public string? Status { get; set; }
            public string? TermsAr { get; set; }
            public string? TermsEn { get; set; }
            public string? ShopName { get; set; }
            public string? ShopLogoPath { get; set; }
            public string? ShopCity { get; set; }
            public string? ShopWhatsAppNumber { get; set; }
            public string? ShopStatus { get; set; }
            public bool? ShowDamaaniBranding { get; set; }
        }
    }
}
