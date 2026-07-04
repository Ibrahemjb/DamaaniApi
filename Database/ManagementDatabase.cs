using System.Data;
using MySql.Data.MySqlClient;

namespace DammaniAPI.Database;

public class ManagementDatabase : IManagementDatabase
{
    private readonly string _connectionString;

    public ManagementDatabase(IConfiguration configuration)
    {
        var raw = configuration["DB_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("DB_CONNECTION_STRING is not configured.");
        _connectionString = MySqlConnectionStringNormalizer.Normalize(raw);
    }

    public IDbConnection Open()
    {
        var connection = new MySqlConnection(_connectionString);
        try
        {
            connection.Open();
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }
}
