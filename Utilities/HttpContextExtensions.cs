namespace DammaniAPI.Utilities;

public static class HttpContextExtensions
{
    public static string? CurrentUserId(this HttpContext context)
        => context.Items["UserId"] as string;

    public static string? CurrentShopId(this HttpContext context)
        => context.Items["ShopId"] as string;

    public static string? CurrentRole(this HttpContext context)
        => context.Items["Role"] as string;

    public static bool IsPlatformAdmin(this HttpContext context)
        => context.Items["IsPlatformAdmin"] as bool? == true;
}
