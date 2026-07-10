using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Branches;

public class GetBranches
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public bool IncludeInactive { get; set; }
    }

    public class BranchItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? City { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Status { get; set; } = BranchStatuses.Active;
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public bool HasBranches { get; set; }
        public List<BranchItem> Branches { get; set; } = new();
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
            var hasBranches = await db.ExecuteScalarAsync<bool?>(
                """
                SELECT p.HasBranches FROM Subscription sub
                JOIN Plan p ON p.Id = sub.PlanId WHERE sub.ShopId = @ShopId
                """,
                new { request.ShopId }) == true;

            var sql = request.IncludeInactive
                ? "SELECT Id, Name, City, Phone, Address, Status FROM Branch WHERE ShopId = @ShopId ORDER BY Name"
                : "SELECT Id, Name, City, Phone, Address, Status FROM Branch WHERE ShopId = @ShopId AND Status = @Active ORDER BY Name";

            var branches = (await db.QueryAsync<BranchItem>(
                sql,
                new { request.ShopId, Active = BranchStatuses.Active })).ToList();

            return new Result { Success = true, HasBranches = hasBranches, Branches = branches };
        }
    }
}
