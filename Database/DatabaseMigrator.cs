using Dapper;
using DbUp;
using MySql.Data.MySqlClient;

namespace DammaniAPI.Database;

public class DatabaseMigrator
{
    private readonly IManagementDatabase _mdb;

    public DatabaseMigrator(IManagementDatabase mdb) => _mdb = mdb;

    public void Migrate()
    {
        var scriptsPath = Path.Combine(AppContext.BaseDirectory, "Database", "Scripts");
        if (!Directory.Exists(scriptsPath))
            throw new DirectoryNotFoundException($"Migration scripts folder not found: {scriptsPath}");

        using var db = _mdb.Open();
        var builder = new MySqlConnectionStringBuilder(((MySqlConnection)db).ConnectionString)
        {
            AllowUserVariables = true
        };
        var connectionString = builder.ConnectionString;
        var databaseName = builder.Database ?? "dammani";

        db.Execute("SELECT GET_LOCK('migration', 1)");
        try
        {
            RunUpgrade(connectionString, databaseName, scriptsPath, path => Path.GetFileName(path) == "00000_init.sql");
            RunUpgrade(connectionString, databaseName, scriptsPath, _ => true);
        }
        finally
        {
            db.Execute("SELECT RELEASE_ALL_LOCKS()");
        }
    }

    private static void RunUpgrade(
        string connectionString,
        string databaseName,
        string scriptsPath,
        Func<string, bool> filter)
    {
        var result = DeployChanges.To
            .MySqlDatabase(connectionString)
            .WithTransactionPerScript()
            .WithScriptsFromFileSystem(scriptsPath, filter)
            .JournalToMySqlTable(databaseName, "schemaversions")
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw result.Error;
    }
}
