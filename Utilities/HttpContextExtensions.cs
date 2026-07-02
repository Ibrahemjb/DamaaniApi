namespace DammaniAPI.Utilities;

public static class HttpContextExtensions
{
    public static string? CurrentUserId(this HttpContext context)
        => context.Items["UserId"] as string;
}
