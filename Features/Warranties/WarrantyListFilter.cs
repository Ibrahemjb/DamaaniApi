using Dapper;

namespace DammaniAPI.Features.Warranties;

// Parameterized WHERE assembly for the warranty list (DMN-405), reused by CSV
// export (DMN-409) so both always agree on filter semantics. Values are always
// bound via parameters — never interpolated.
public static class WarrantyListFilter
{
    public class Args
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? ExpiryFrom { get; set; }
        public DateTime? ExpiryTo { get; set; }
        public string? BranchId { get; set; }
        public string? CreatedByUserId { get; set; }
    }

    // Derived status expression shared by SELECT and status filtering.
    public const string DerivedStatusSql =
        "CASE WHEN w.Status = 'active' AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE() THEN 'expired' ELSE w.Status END";

    public static (string WhereSql, DynamicParameters Parameters) Build(string shopId, Args args)
    {
        var where = new List<string> { "w.ShopId = @ShopId" };
        var parameters = new DynamicParameters();
        parameters.Add("ShopId", shopId);

        var search = args.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (search.Length > 100) search = search[..100];
            var escaped = EscapeLike(search);
            var clauses = new List<string>
            {
                "w.Code LIKE @SearchPrefix",
                "w.SerialNumber LIKE @SearchPrefix",
                "c.Name LIKE @SearchContains",
                "w.ProductName LIKE @SearchContains"
            };
            parameters.Add("SearchPrefix", escaped + "%");
            parameters.Add("SearchContains", "%" + escaped + "%");

            // Customer.Phone is stored normalized (digits, optional leading +),
            // so a digits-only version of the term matches any typed format.
            var digits = new string(search.Where(char.IsDigit).ToArray());
            if (digits.Length >= 3)
            {
                clauses.Add("c.Phone LIKE @SearchPhone");
                parameters.Add("SearchPhone", "%" + digits + "%");
            }

            where.Add("(" + string.Join(" OR ", clauses) + ")");
        }

        switch (args.Status)
        {
            case WarrantyStatuses.Draft:
            case WarrantyStatuses.Cancelled:
                where.Add("w.Status = @Status");
                parameters.Add("Status", args.Status);
                break;
            case WarrantyStatuses.Active:
                where.Add("w.Status = 'active' AND (w.ExpiryDate IS NULL OR w.ExpiryDate >= CURDATE())");
                break;
            case "expired":
                where.Add("w.Status = 'active' AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()");
                break;
        }

        if (!string.IsNullOrWhiteSpace(args.Category))
        {
            where.Add("w.Category = @Category");
            parameters.Add("Category", args.Category);
        }
        if (args.CreatedFrom.HasValue)
        {
            where.Add("w.CreatedAt >= @CreatedFrom");
            parameters.Add("CreatedFrom", args.CreatedFrom.Value.Date);
        }
        if (args.CreatedTo.HasValue)
        {
            where.Add("w.CreatedAt < @CreatedTo");
            parameters.Add("CreatedTo", args.CreatedTo.Value.Date.AddDays(1));
        }
        if (args.ExpiryFrom.HasValue)
        {
            where.Add("w.ExpiryDate >= @ExpiryFrom");
            parameters.Add("ExpiryFrom", args.ExpiryFrom.Value.Date);
        }
        if (args.ExpiryTo.HasValue)
        {
            where.Add("w.ExpiryDate <= @ExpiryTo");
            parameters.Add("ExpiryTo", args.ExpiryTo.Value.Date);
        }
        if (!string.IsNullOrWhiteSpace(args.BranchId))
        {
            where.Add("w.BranchId = @BranchId");
            parameters.Add("BranchId", args.BranchId);
        }
        if (!string.IsNullOrWhiteSpace(args.CreatedByUserId))
        {
            where.Add("w.CreatedByUserId = @CreatedByUserId");
            parameters.Add("CreatedByUserId", args.CreatedByUserId);
        }

        return (string.Join(" AND ", where), parameters);
    }

    internal static string EscapeLike(string term)
        => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
