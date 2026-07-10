using Dapper;

namespace DammaniAPI.Features.ServiceRequests;

// Parameterized WHERE for the service-request list (DMN-601), mirroring
// WarrantyListFilter (DMN-405). Values are always bound — never interpolated.
public static class ServiceRequestListFilter
{
    public class Args
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Category { get; set; }
        public string? WarrantyStatus { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? BranchId { get; set; }
    }

    public static (string WhereSql, DynamicParameters Parameters) Build(string shopId, Args args)
    {
        var where = new List<string> { "sr.ShopId = @ShopId" };
        var parameters = new DynamicParameters();
        parameters.Add("ShopId", shopId);

        var search = args.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (search.Length > 100) search = search[..100];
            var escaped = EscapeLike(search);
            var clauses = new List<string>
            {
                "sr.RequestNumber LIKE @SearchPrefix",
                "sr.CustomerName LIKE @SearchContains",
                "w.Code LIKE @SearchPrefix",
                "w.ProductName LIKE @SearchContains",
                "w.SerialNumber LIKE @SearchPrefix"
            };
            parameters.Add("SearchPrefix", escaped + "%");
            parameters.Add("SearchContains", "%" + escaped + "%");

            var digits = new string(search.Where(char.IsDigit).ToArray());
            if (digits.Length >= 3)
            {
                clauses.Add("sr.CustomerPhone LIKE @SearchPhone");
                parameters.Add("SearchPhone", "%" + digits + "%");
            }

            where.Add("(" + string.Join(" OR ", clauses) + ")");
        }

        if (!string.IsNullOrWhiteSpace(args.Status)
            && ServiceRequestStatuses.Supported.Contains(args.Status))
        {
            where.Add("sr.Status = @Status");
            parameters.Add("Status", args.Status);
        }

        if (args.DateFrom.HasValue)
        {
            where.Add("sr.CreatedAt >= @DateFrom");
            parameters.Add("DateFrom", args.DateFrom.Value.Date);
        }
        if (args.DateTo.HasValue)
        {
            where.Add("sr.CreatedAt < @DateTo");
            parameters.Add("DateTo", args.DateTo.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(args.Category))
        {
            where.Add("w.Category = @Category");
            parameters.Add("Category", args.Category);
        }

        switch (args.WarrantyStatus)
        {
            case WarrantyStatuses.Active:
                where.Add("w.Status = 'active' AND (w.ExpiryDate IS NULL OR w.ExpiryDate >= CURDATE())");
                break;
            case "expired":
                where.Add("w.Status = 'active' AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()");
                break;
        }

        if (!string.IsNullOrWhiteSpace(args.AssignedToUserId))
        {
            where.Add("sr.AssignedToUserId = @AssignedToUserId");
            parameters.Add("AssignedToUserId", args.AssignedToUserId);
        }

        if (!string.IsNullOrWhiteSpace(args.BranchId))
        {
            where.Add("w.BranchId = @BranchId");
            parameters.Add("BranchId", args.BranchId);
        }

        return (string.Join(" AND ", where), parameters);
    }

    internal static string EscapeLike(string term)
        => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
