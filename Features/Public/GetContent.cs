using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Public;

public class GetContent
{
    public class Query : IRequest<Result>
    {
        public string[] Keys { get; set; } = [];
    }

    public class Block
    {
        public string BlockKey { get; set; } = "";
        public string? TitleAr { get; set; }
        public string? TitleEn { get; set; }
        public string? BodyAr { get; set; }
        public string? BodyEn { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<Block> Blocks { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (request.Keys.Length == 0)
                return new Result();

            using var db = _mdb.Open();
            var blocks = (await db.QueryAsync<Block>(
                """
                SELECT BlockKey, TitleAr, TitleEn, BodyAr, BodyEn
                FROM ContentBlock
                WHERE IsPublished = 1 AND BlockKey IN @Keys
                ORDER BY SortOrder
                """,
                new { request.Keys })).ToList();

            return new Result { Blocks = blocks };
        }
    }
}
