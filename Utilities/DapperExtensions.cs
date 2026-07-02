using System.Data;
using Dapper;

namespace DammaniAPI.Utilities;

public static class DapperExtensions
{
    public static async Task<int> InsertDynamicAsync(
        this IDbConnection db,
        string table,
        object values,
        IDbTransaction? transaction = null)
    {
        var props = values.GetType().GetProperties();
        var columns = string.Join(", ", props.Select(p => p.Name));
        var parameters = string.Join(", ", props.Select(p => "@" + p.Name));
        var sql = $"INSERT INTO {table} ({columns}) VALUES ({parameters})";
        return await db.ExecuteAsync(sql, values, transaction);
    }

    public static async Task<int> UpdateDynamicAsync(
        this IDbConnection db,
        string table,
        object values,
        string whereClause,
        IDbTransaction? transaction = null)
    {
        var props = values.GetType().GetProperties();
        var setClause = string.Join(", ", props.Select(p => $"{p.Name} = @{p.Name}"));
        var sql = $"UPDATE {table} SET {setClause} WHERE {whereClause}";
        return await db.ExecuteAsync(sql, values, transaction);
    }
}
