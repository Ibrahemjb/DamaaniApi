using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Auth;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class GetInvite
{
    public class Query : IRequest<Result>
    {
        public string? Token { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public bool Valid { get; set; }
        public string? ShopName { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return new Result { Success = true, Valid = false };

            using var db = _mdb.Open();
            var row = await db.QueryFirstOrDefaultAsync<(string ShopName, DateTime ExpiresAt, DateTime? AcceptedAt)?>(
                """
                SELECT s.Name AS ShopName, i.ExpiresAt, i.AcceptedAt
                FROM StaffInvite i
                JOIN Shop s ON s.Id = i.ShopId
                WHERE i.TokenHash = @TokenHash
                """,
                new { TokenHash = RequestPasswordReset.HashToken(request.Token.Trim()) });

            if (row == null || row.Value.AcceptedAt != null || row.Value.ExpiresAt <= DateTime.UtcNow)
                return new Result { Success = true, Valid = false };

            return new Result { Success = true, Valid = true, ShopName = row.Value.ShopName };
        }
    }
}
