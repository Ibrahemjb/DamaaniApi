using Dapper;
using DammaniAPI.Features.Messaging;
using System.Data;

namespace DammaniAPI.Features.Messaging;

// Resolution order (DMN-410/1104): shop MessageTemplate → PlatformMessage → code default.
public static class MessageResolver
{
    public static async Task<(string Ar, string En)> ResolveAsync(
        IDbConnection db,
        IDbTransaction? tx,
        string? shopId,
        string templateKey)
    {
        if (!DefaultMessages.Defaults.TryGetValue(templateKey, out var fallback))
            return ("", "");

        if (!string.IsNullOrWhiteSpace(shopId))
        {
            var shopRow = await db.QueryFirstOrDefaultAsync<(string? TextAr, string? TextEn)>(
                "SELECT TextAr, TextEn FROM MessageTemplate WHERE ShopId = @ShopId AND TemplateKey = @TemplateKey",
                new { ShopId = shopId, TemplateKey = templateKey },
                tx);
            if (shopRow.TextAr is not null || shopRow.TextEn is not null)
            {
                return (
                    string.IsNullOrWhiteSpace(shopRow.TextAr) ? fallback.Ar : shopRow.TextAr,
                    string.IsNullOrWhiteSpace(shopRow.TextEn) ? fallback.En : shopRow.TextEn);
            }
        }

        var platform = await db.QueryFirstOrDefaultAsync<(string? TextAr, string? TextEn)>(
            "SELECT TextAr, TextEn FROM PlatformMessage WHERE TemplateKey = @TemplateKey",
            new { TemplateKey = templateKey },
            tx);
        if (platform.TextAr is not null || platform.TextEn is not null)
        {
            return (
                string.IsNullOrWhiteSpace(platform.TextAr) ? fallback.Ar : platform.TextAr,
                string.IsNullOrWhiteSpace(platform.TextEn) ? fallback.En : platform.TextEn);
        }

        return (fallback.Ar, fallback.En);
    }
}
