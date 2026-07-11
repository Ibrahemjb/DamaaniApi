using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetContactMessages
{
    public class Query : IRequest<Result>
    {
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class MessageRow
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public string Email { get; set; } = "";
        public string? Topic { get; set; }
        public string Message { get; set; } = "";
        public string? ShopId { get; set; }
        public string? ShopName { get; set; }
        public string Status { get; set; } = "";
        public string? InternalNote { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<MessageRow> Items { get; set; } = new();
        public int Total { get; set; }
        public int UnreadCount { get; set; }
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
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var offset = (page - 1) * pageSize;
            var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToLowerInvariant();

            using var db = _mdb.Open();
            var unread = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ContactMessage WHERE Status = @Unread",
                new { Unread = ContactMessageStatuses.Unread });

            var total = await db.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM ContactMessage
                WHERE (@Status IS NULL OR Status = @Status)
                """,
                new { Status = status });

            var items = (await db.QueryAsync<MessageRow>(
                """
                SELECT cm.Id, cm.Name, cm.Email, cm.Topic, cm.Message, cm.ShopId,
                       COALESCE(s.Name, matched.Name) AS ShopName,
                       cm.Status, cm.InternalNote, cm.ResolvedAt, cm.CreatedAt
                FROM ContactMessage cm
                LEFT JOIN Shop s ON s.Id = cm.ShopId
                LEFT JOIN (
                    SELECT su.UserId, sh.Id, sh.Name, u.Email
                    FROM ShopUser su
                    JOIN Shop sh ON sh.Id = su.ShopId
                    JOIN User u ON u.Id = su.UserId
                    WHERE su.Role = 'owner'
                ) matched ON LOWER(matched.Email) = LOWER(cm.Email)
                WHERE (@Status IS NULL OR cm.Status = @Status)
                ORDER BY
                    CASE cm.Status WHEN 'unread' THEN 0 WHEN 'in_progress' THEN 1 ELSE 2 END,
                    cm.CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                new { Status = status, PageSize = pageSize, Offset = offset })).ToList();

            return new Result
            {
                Items = items,
                Total = total,
                UnreadCount = unread,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}

public class UpdateContactMessage
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string Id { get; set; } = "";
        public string Status { get; set; } = "";
        public string? InternalNote { get; set; }
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
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Status).Must(s =>
                s is ContactMessageStatuses.Unread or ContactMessageStatuses.InProgress or ContactMessageStatuses.Closed);
            RuleFor(x => x.InternalNote).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.InternalNote));
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
                "SELECT COUNT(*) FROM ContactMessage WHERE Id = @Id", new { request.Id });
            if (exists == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var closed = string.Equals(request.Status, ContactMessageStatuses.Closed, StringComparison.OrdinalIgnoreCase);
            await db.ExecuteAsync(
                """
                UPDATE ContactMessage
                SET Status = @Status,
                    InternalNote = @InternalNote,
                    ResolvedAt = CASE WHEN @Closed = 1 THEN UTC_TIMESTAMP() ELSE NULL END,
                    ResolvedByUserId = CASE WHEN @Closed = 1 THEN @ActorUserId ELSE NULL END
                WHERE Id = @Id
                """,
                new
                {
                    request.Id,
                    request.Status,
                    request.InternalNote,
                    Closed = closed ? 1 : 0,
                    request.ActorUserId
                });

            await ActivityLogger.LogAsync(
                db, null, null, "contact_message", request.Id, "contact.status_updated",
                request.ActorUserId, $"{{\"status\":\"{request.Status}\"}}");

            return new Result { Success = true };
        }
    }
}
