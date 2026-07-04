using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using MediatR;

namespace DammaniAPI.Features.Auth;

public class GetMe
{
    public class Query : IRequest<Result>
    {
        public string? UserId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public AuthUserResult? User { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            var user = await db.QueryFirstOrDefaultAsync<AuthUserResult>(
                """
                SELECT
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.Language,
                    su.Role,
                    su.ShopId,
                    u.IsPlatformAdmin
                FROM User u
                LEFT JOIN ShopUser su ON su.UserId = u.Id AND su.Status = 'active'
                WHERE u.Id = @UserId
                ORDER BY su.Role = 'owner' DESC
                LIMIT 1
                """,
                new { request.UserId });

            return user == null
                ? new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized }
                : new Result { Success = true, User = user };
        }
    }
}
