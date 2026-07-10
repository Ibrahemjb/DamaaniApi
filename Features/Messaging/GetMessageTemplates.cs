using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Messaging;

public class GetMessageTemplates
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
    }

    public class TemplateText
    {
        public string Ar { get; set; } = "";
        public string En { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public Dictionary<string, TemplateText> Templates { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var result = new Result { Success = true };
            foreach (var (key, text) in DefaultMessages.Defaults)
                result.Templates[key] = new TemplateText { Ar = text.Ar, En = text.En };

            // Per-shop overrides live in MessageTemplate, which arrives with
            // DMN-901. Guarding on information_schema lets shops customize via
            // rows the moment that migration lands, without redeploying this API.
            using var db = _mdb.Open();
            var hasTemplateTable = await db.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_schema = DATABASE() AND table_name = 'MessageTemplate'
                """);
            if (hasTemplateTable > 0)
            {
                var rows = await db.QueryAsync<(string TemplateKey, string? TextAr, string? TextEn)>(
                    "SELECT TemplateKey, TextAr, TextEn FROM MessageTemplate WHERE ShopId = @ShopId",
                    new { request.ShopId });
                foreach (var row in rows)
                {
                    if (!result.Templates.TryGetValue(row.TemplateKey, out var text))
                        continue;
                    if (!string.IsNullOrWhiteSpace(row.TextAr)) text.Ar = row.TextAr;
                    if (!string.IsNullOrWhiteSpace(row.TextEn)) text.En = row.TextEn;
                }
            }

            return result;
        }
    }
}
