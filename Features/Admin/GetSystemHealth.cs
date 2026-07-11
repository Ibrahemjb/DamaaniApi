using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetSystemHealth
{
    public class Query : IRequest<Result> { }

    public class Result
    {
        public bool Success { get; set; } = true;
        public string Status { get; set; } = "healthy";
        public bool DatabaseOk { get; set; }
        public string? LatestMigration { get; set; }
        public int MigrationCount { get; set; }
        public int TotalShops { get; set; }
        public int ActiveShops { get; set; }
        public int SuspendedShops { get; set; }
        public int OpenServiceRequests { get; set; }
        public int PendingUpgrades { get; set; }
        public DateTime ServerTimeUtc { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            var result = new Result { ServerTimeUtc = DateTime.UtcNow };
            try
            {
                using var db = _mdb.Open();
                await db.ExecuteScalarAsync<int>("SELECT 1");
                result.DatabaseOk = true;

                try
                {
                    var latest = await db.QueryFirstOrDefaultAsync<(string ScriptName, DateTime Applied)>(
                        """
                        SELECT ScriptName, Applied
                        FROM schemaversions
                        ORDER BY Applied DESC
                        LIMIT 1
                        """);
                    result.LatestMigration = latest.ScriptName;
                    result.MigrationCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM schemaversions");
                }
                catch
                {
                    result.LatestMigration = null;
                }

                result.TotalShops = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Shop");
                result.ActiveShops = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Shop WHERE Status = 'active'");
                result.SuspendedShops = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Shop WHERE Status = 'suspended'");
                result.OpenServiceRequests = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM ServiceRequest WHERE Status <> 'closed'");
                result.PendingUpgrades = await db.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*)
                    FROM Subscription sub
                    JOIN Plan cur ON cur.Id = sub.PlanId
                    JOIN Plan sched ON sched.Id = sub.ScheduledPlanId
                    WHERE sched.SortOrder > cur.SortOrder
                    """);

                result.Status = result.DatabaseOk ? "healthy" : "degraded";
            }
            catch
            {
                result.DatabaseOk = false;
                result.Status = "unhealthy";
                result.Success = true;
            }

            return result;
        }
    }
}
