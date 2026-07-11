using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetAdminUsers
{
    public class Query : IRequest<Result> { }

    public class AdminUserRow
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string AdminRole { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<AdminUserRow> Items { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var items = (await db.QueryAsync<AdminUserRow>(
                """
                SELECT Id, FullName, Email,
                       COALESCE(AdminRole, 'super') AS AdminRole,
                       Status, CreatedAt
                FROM User
                WHERE IsPlatformAdmin = 1
                ORDER BY CreatedAt ASC
                """)).ToList();
            return new Result { Items = items };
        }
    }
}

public class SetAdminRole
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string UserId { get; set; } = "";
        public string AdminRole { get; set; } = "";
        public bool IsPlatformAdmin { get; set; } = true;
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.AdminRole).Must(r => AdminRoles.All.Contains(r)).When(x => x.IsPlatformAdmin);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var exists = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM User WHERE Id = @UserId", new { request.UserId });
            if (exists == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            // Prevent removing the last super admin
            if (!request.IsPlatformAdmin || !string.Equals(request.AdminRole, AdminRoles.Super, StringComparison.OrdinalIgnoreCase))
            {
                var otherSupers = await db.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*) FROM User
                    WHERE IsPlatformAdmin = 1
                      AND COALESCE(AdminRole, 'super') = 'super'
                      AND Id <> @UserId
                    """,
                    new { request.UserId });
                if (otherSupers == 0)
                    return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };
            }

            await db.ExecuteAsync(
                """
                UPDATE User
                SET IsPlatformAdmin = @IsPlatformAdmin,
                    AdminRole = CASE WHEN @IsPlatformAdmin = 1 THEN @AdminRole ELSE NULL END,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @UserId
                """,
                new
                {
                    request.UserId,
                    IsPlatformAdmin = request.IsPlatformAdmin ? 1 : 0,
                    AdminRole = AdminRoles.Normalize(request.AdminRole)
                });

            await ActivityLogger.LogAsync(
                db, null, null, "user", request.UserId, "admin.role_updated",
                request.ActorUserId,
                $"{{\"role\":\"{request.AdminRole}\",\"isPlatformAdmin\":{request.IsPlatformAdmin.ToString().ToLowerInvariant()}}}");

            return new Result { Success = true };
        }
    }
}
