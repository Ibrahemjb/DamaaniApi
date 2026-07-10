using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetContentBlocks
{
    public class Query : IRequest<Result> { }

    public class BlockRow
    {
        public string Id { get; set; } = "";
        public string BlockKey { get; set; } = "";
        public string? TitleAr { get; set; }
        public string? TitleEn { get; set; }
        public string? BodyAr { get; set; }
        public string? BodyEn { get; set; }
        public int SortOrder { get; set; }
        public bool IsPublished { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<BlockRow> Items { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var items = (await db.QueryAsync<BlockRow>(
                """
                SELECT Id, BlockKey, TitleAr, TitleEn, BodyAr, BodyEn, SortOrder, IsPublished
                FROM ContentBlock
                ORDER BY SortOrder, BlockKey
                """)).ToList();
            return new Result { Items = items };
        }
    }
}

public class UpdateContentBlock
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string BlockKey { get; set; } = "";
        public string? TitleAr { get; set; }
        public string? TitleEn { get; set; }
        public string? BodyAr { get; set; }
        public string? BodyEn { get; set; }
        public bool IsPublished { get; set; } = true;
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.BlockKey).NotEmpty().MaximumLength(60);
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var existing = await db.QueryFirstOrDefaultAsync<string?>(
                "SELECT Id FROM ContentBlock WHERE BlockKey = @BlockKey",
                new { request.BlockKey });

            if (existing is null)
            {
                await db.ExecuteAsync(
                    """
                    INSERT INTO ContentBlock
                        (Id, BlockKey, TitleAr, TitleEn, BodyAr, BodyEn, SortOrder, IsPublished, CreatedAt, UpdatedAt)
                    VALUES
                        (@Id, @BlockKey, @TitleAr, @TitleEn, @BodyAr, @BodyEn, 0, @IsPublished, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                    """,
                    new { Id = Guid.NewGuid().ToString(), request.BlockKey, request.TitleAr, request.TitleEn, request.BodyAr, request.BodyEn, request.IsPublished });
            }
            else
            {
                await db.ExecuteAsync(
                    """
                    UPDATE ContentBlock
                    SET TitleAr = @TitleAr, TitleEn = @TitleEn, BodyAr = @BodyAr, BodyEn = @BodyEn,
                        IsPublished = @IsPublished, UpdatedAt = UTC_TIMESTAMP()
                    WHERE BlockKey = @BlockKey
                    """,
                    request);
            }

            await ActivityLogger.LogAsync(
                db, null, null, "content_block", request.BlockKey, "content.block_updated",
                request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
