using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Settings;

// Single fetch for the whole settings screen (DMN-902–905).
public class GetShopSettings
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Name { get; set; }
        public string? LogoPath { get; set; }
        public string? Phone { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string Country { get; set; } = "PS";
        public string? BusinessCategory { get; set; }
        public string PublicLanguage { get; set; } = Languages.Arabic;
        public string PublicTheme { get; set; } = "default";
        public bool PublicShowAddress { get; set; } = true;
        public bool PublicShowWhatsApp { get; set; } = true;
        public int? DefaultWarrantyDurationMonths { get; set; }
        public bool AllowExpiredRequests { get; set; } = true;
        public bool EmailAlertsEnabled { get; set; }
        public string? AlertEmail { get; set; }
        public int MaxUsers { get; set; } = 1;
        public bool HasBranches { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            var row = await db.QueryFirstOrDefaultAsync<Result>(
                """
                SELECT s.Name, s.LogoPath, s.Phone, s.WhatsAppNumber, s.Address, s.City, s.Country,
                       s.BusinessCategory, s.PublicLanguage, s.PublicTheme,
                       s.PublicShowAddress, s.PublicShowWhatsApp,
                       s.DefaultWarrantyDurationMonths, s.AllowExpiredRequests,
                       s.EmailAlertsEnabled, s.AlertEmail,
                       COALESCE(p.MaxUsers, 1) AS MaxUsers,
                       COALESCE(p.HasBranches, 0) AS HasBranches
                FROM Shop s
                LEFT JOIN Subscription sub ON sub.ShopId = s.Id
                LEFT JOIN Plan p ON p.Id = sub.PlanId
                WHERE s.Id = @ShopId
                """,
                new { request.ShopId });

            if (row == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            row.Success = true;
            return row;
        }
    }
}
