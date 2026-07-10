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

    public class TemplateItem
    {
        public string Key { get; set; } = "";
        public string Ar { get; set; } = "";
        public string En { get; set; } = "";
        public bool IsCustomized { get; set; }
        public string DefaultAr { get; set; } = "";
        public string DefaultEn { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public Dictionary<string, TemplateText> Templates { get; set; } = new();
        public List<TemplateItem> Items { get; set; } = new();
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
            var customized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, text) in DefaultMessages.Defaults)
            {
                result.Templates[key] = new TemplateText { Ar = text.Ar, En = text.En };
                result.Items.Add(new TemplateItem
                {
                    Key = key,
                    Ar = text.Ar,
                    En = text.En,
                    DefaultAr = text.Ar,
                    DefaultEn = text.En
                });
            }

            using var db = _mdb.Open();
            var platformRows = await db.QueryAsync<(string TemplateKey, string? TextAr, string? TextEn)>(
                "SELECT TemplateKey, TextAr, TextEn FROM PlatformMessage");
            foreach (var row in platformRows)
            {
                if (!result.Templates.TryGetValue(row.TemplateKey, out var text))
                    continue;
                if (!string.IsNullOrWhiteSpace(row.TextAr)) text.Ar = row.TextAr;
                if (!string.IsNullOrWhiteSpace(row.TextEn)) text.En = row.TextEn;
            }

            var rows = await db.QueryAsync<(string TemplateKey, string? TextAr, string? TextEn)>(
                "SELECT TemplateKey, TextAr, TextEn FROM MessageTemplate WHERE ShopId = @ShopId",
                new { request.ShopId });
            foreach (var row in rows)
            {
                if (!result.Templates.TryGetValue(row.TemplateKey, out var text))
                    continue;
                customized.Add(row.TemplateKey);
                if (!string.IsNullOrWhiteSpace(row.TextAr)) text.Ar = row.TextAr;
                if (!string.IsNullOrWhiteSpace(row.TextEn)) text.En = row.TextEn;
            }

            foreach (var item in result.Items)
            {
                if (result.Templates.TryGetValue(item.Key, out var effective))
                {
                    item.Ar = effective.Ar;
                    item.En = effective.En;
                }
                item.IsCustomized = customized.Contains(item.Key);
            }

            return result;
        }
    }
}
