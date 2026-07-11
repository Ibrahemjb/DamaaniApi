namespace DammaniAPI.Features.Admin;

public static class AdminRoles
{
    public const string Super = "super";
    public const string Billing = "billing";
    public const string Support = "support";
    public const string Content = "content";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Super, Billing, Support, Content
    };

    public static string Normalize(string? role)
        => string.IsNullOrWhiteSpace(role) ? Super : role.Trim().ToLowerInvariant();

    public static bool Allows(string? actual, params string[] required)
    {
        var role = Normalize(actual);
        if (role == Super)
            return true;
        return required.Any(r => string.Equals(role, r, StringComparison.OrdinalIgnoreCase));
    }
}

public static class ContactMessageStatuses
{
    public const string Unread = "unread";
    public const string InProgress = "in_progress";
    public const string Closed = "closed";
}
