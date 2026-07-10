namespace DammaniAPI.Features.Messaging;

// Allowed {variable} placeholders in shop message templates (BP §12).
public static class MessageTemplateVars
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer_name",
        "product_name",
        "warranty_code",
        "expiry_date",
        "public_link",
        "shop_name",
        "request_number"
    };

    public static string? FindUnknown(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var start = 0;
        while (start < text.Length)
        {
            var open = text.IndexOf('{', start);
            if (open < 0) break;
            var close = text.IndexOf('}', open + 1);
            if (close < 0) break;
            var name = text[(open + 1)..close].Trim();
            if (!string.IsNullOrEmpty(name) && !Allowed.Contains(name))
                return name;
            start = close + 1;
        }
        return null;
    }
}
