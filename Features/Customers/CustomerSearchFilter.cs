using Dapper;

namespace DammaniAPI.Features.Customers;

// Parameterized WHERE for customer lookup search (DMN-701).
public static class CustomerSearchFilter
{
    public static (string WhereSql, DynamicParameters Parameters) Build(string shopId, string term)
    {
        var where = new List<string> { "c.ShopId = @ShopId" };
        var parameters = new DynamicParameters();
        parameters.Add("ShopId", shopId);

        var search = term.Trim();
        if (search.Length > 100) search = search[..100];
        var escaped = EscapeLike(search);
        var clauses = new List<string> { "c.Name LIKE @SearchContains" };
        parameters.Add("SearchContains", "%" + escaped + "%");

        var digits = new string(search.Where(char.IsDigit).ToArray());
        if (digits.Length >= 2)
        {
            clauses.Add("c.Phone LIKE @SearchPhone");
            parameters.Add("SearchPhone", "%" + digits + "%");
        }

        where.Add("(" + string.Join(" OR ", clauses) + ")");
        return (string.Join(" AND ", where), parameters);
    }

    internal static string EscapeLike(string term)
        => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
