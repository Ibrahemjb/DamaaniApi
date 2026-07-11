using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetActivity
{
    public class Query : IRequest<Result>
    {
        public string? Action { get; set; }
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ActivityRow
    {
        public string Id { get; set; } = "";
        public string? ShopId { get; set; }
        public string? ShopName { get; set; }
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "";
        public string? ActorUserId { get; set; }
        public string? ActorEmail { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<ActivityRow> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var offset = (page - 1) * pageSize;
            var action = string.IsNullOrWhiteSpace(request.Action) ? null : $"%{request.Action.Trim()}%";

            using var db = _mdb.Open();
            var total = await db.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM ActivityLog a
                WHERE (@Action IS NULL OR a.Action LIKE @Action)
                  AND (@ShopId IS NULL OR a.ShopId = @ShopId)
                  AND (@ActorUserId IS NULL OR a.ActorUserId = @ActorUserId)
                  AND (@From IS NULL OR a.CreatedAt >= @From)
                  AND (@To IS NULL OR a.CreatedAt <= @To)
                """,
                new
                {
                    Action = action,
                    request.ShopId,
                    request.ActorUserId,
                    request.From,
                    request.To
                });

            var items = (await db.QueryAsync<ActivityRow>(
                """
                SELECT a.Id, a.ShopId, s.Name AS ShopName, a.EntityType, a.EntityId, a.Action,
                       a.ActorUserId, u.Email AS ActorEmail, CAST(a.Details AS CHAR) AS Details, a.CreatedAt
                FROM ActivityLog a
                LEFT JOIN Shop s ON s.Id = a.ShopId
                LEFT JOIN User u ON u.Id = a.ActorUserId
                WHERE (@Action IS NULL OR a.Action LIKE @Action)
                  AND (@ShopId IS NULL OR a.ShopId = @ShopId)
                  AND (@ActorUserId IS NULL OR a.ActorUserId = @ActorUserId)
                  AND (@From IS NULL OR a.CreatedAt >= @From)
                  AND (@To IS NULL OR a.CreatedAt <= @To)
                ORDER BY a.CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                new
                {
                    Action = action,
                    request.ShopId,
                    request.ActorUserId,
                    request.From,
                    request.To,
                    PageSize = pageSize,
                    Offset = offset
                })).ToList();

            return new Result { Items = items, Total = total, Page = page, PageSize = pageSize };
        }
    }
}
