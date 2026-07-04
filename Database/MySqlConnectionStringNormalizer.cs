using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace DammaniAPI.Database;

public static class MySqlConnectionStringNormalizer
{
    /// <summary>
    /// MySql.Data / DbUp (MySqlConnector) expect Uid and Pwd — not User=.
    /// </summary>
    public static string Normalize(string raw)
    {
        raw = Regex.Replace(raw, @"(?<![\w\s])User=", "Uid=", RegexOptions.IgnoreCase);
        raw = Regex.Replace(raw, @"Allow\s+User\s+Variables", "AllowUserVariables", RegexOptions.IgnoreCase);

        var builder = new MySqlConnectionStringBuilder(raw)
        {
            AllowUserVariables = true
        };

        if (string.IsNullOrWhiteSpace(builder.UserID))
            throw new InvalidOperationException(
                "DB_CONNECTION_STRING must include Uid= (or User ID=). 'User=' alone is not recognized.");

        return builder.ConnectionString;
    }
}
