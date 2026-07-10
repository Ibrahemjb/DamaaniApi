using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Warranties;

// Serial duplicates WARN, never block (BP §10.9). The create form calls this on
// blur; CreateWarranty runs the same check server-side unless acknowledged.
public class CheckSerialDuplicate
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? SerialNumber { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public bool IsDuplicate { get; set; }
        public string? ExistingWarrantyCode { get; set; }
        public string? ExistingProductName { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var serial = request.SerialNumber?.Trim();
            if (string.IsNullOrEmpty(serial))
                return new Result { Success = true, IsDuplicate = false };

            using var db = _mdb.Open();
            var existing = await FindAsync(db, null, request.ShopId, serial);

            return new Result
            {
                Success = true,
                IsDuplicate = existing != null,
                ExistingWarrantyCode = existing?.Code,
                ExistingProductName = existing?.ProductName
            };
        }
    }

    public class ExistingWarranty
    {
        public string? Code { get; set; }
        public string? ProductName { get; set; }
    }

    // Shared with CreateWarranty's submit-time check: same shop, non-cancelled,
    // case-insensitive on the trimmed serial (column collation is CI).
    internal static async Task<ExistingWarranty?> FindAsync(
        System.Data.IDbConnection db, System.Data.IDbTransaction? tx, string shopId, string serial)
    {
        return await db.QueryFirstOrDefaultAsync<ExistingWarranty>(
            """
            SELECT Code, ProductName
            FROM Warranty
            WHERE ShopId = @ShopId
              AND Status <> @Cancelled
              AND SerialNumber = @Serial
            ORDER BY CreatedAt DESC
            LIMIT 1
            """,
            new { ShopId = shopId, Cancelled = WarrantyStatuses.Cancelled, Serial = serial },
            tx);
    }
}
