using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class GetStaff
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class Member
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class PendingInvite
    {
        public string Id { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public List<Member> Members { get; set; } = new();
        public List<PendingInvite> PendingInvites { get; set; } = new();
        public int MaxUsers { get; set; } = 1;
        public int UsedSlots { get; set; }
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
            var maxUsers = await db.ExecuteScalarAsync<int?>(
                """
                SELECT p.MaxUsers FROM Subscription sub
                JOIN Plan p ON p.Id = sub.PlanId WHERE sub.ShopId = @ShopId
                """,
                new { request.ShopId }) ?? 1;

            var members = (await db.QueryAsync<Member>(
                """
                SELECT u.Id AS UserId, u.FullName, u.Email, u.Phone, su.Role, su.Status
                FROM ShopUser su
                JOIN User u ON u.Id = su.UserId
                WHERE su.ShopId = @ShopId
                ORDER BY su.Role = 'owner' DESC, u.FullName
                """,
                new { request.ShopId })).ToList();

            var pending = (await db.QueryAsync<PendingInvite>(
                """
                SELECT Id, Email, Phone, ExpiresAt
                FROM StaffInvite
                WHERE ShopId = @ShopId AND AcceptedAt IS NULL AND ExpiresAt > UTC_TIMESTAMP()
                ORDER BY CreatedAt DESC
                """,
                new { request.ShopId })).ToList();

            var usedSlots = members.Count(m =>
                    string.Equals(m.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
                + pending.Count;

            return new Result
            {
                Success = true,
                Members = members,
                PendingInvites = pending,
                MaxUsers = maxUsers,
                UsedSlots = usedSlots
            };
        }
    }
}
