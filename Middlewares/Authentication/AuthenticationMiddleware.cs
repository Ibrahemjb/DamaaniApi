namespace DammaniAPI.Middlewares.Authentication;

public class AuthenticationMiddleware
{
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health"
    };

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public AuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (PublicPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        // ponytail: JWT validation deferred until JWKS is configured; upgrade path is full Bearer + JWKS check
        var issuer = _configuration["JWT_ISSUER_URL"];
        var audience = _configuration["JWT_APP_IDENTIFIER"];
        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // ponytail: stub — wire JWT validation against JWKS when identity provider is ready
        context.Items["UserId"] = "anonymous";
        context.Items["Scope"] = "";
        await _next(context);
    }
}
