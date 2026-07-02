using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Users;

public class CheckIfUserExists
{
    public class Query : IRequest<Result>
    {
        public string? UserId { get; set; }
    }

    public class Result
    {
        public bool UserExists { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var id = await db.QueryFirstOrDefaultAsync<string?>(
                "SELECT Id FROM User WHERE Id = @UserId",
                new { UserId = request.UserId ?? "" });
            return new Result { UserExists = id != null };
        }
    }
}
