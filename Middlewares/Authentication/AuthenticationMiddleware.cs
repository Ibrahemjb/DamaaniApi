using System.Security.Claims;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace DammaniAPI.Middlewares.Authentication;

public class AuthenticationMiddleware
{
    private static readonly HashSet<string> PublicPostAuthPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/signup",
        "/auth/login",
        "/auth/requestPasswordReset",
        "/auth/resetPassword"
    };

    private readonly RequestDelegate _next;
    private readonly ITokenService _tokenService;
    private readonly IManagementDatabase _mdb;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _environment;

    public AuthenticationMiddleware(
        RequestDelegate next,
        ITokenService tokenService,
        IManagementDatabase mdb,
        IMemoryCache cache,
        IWebHostEnvironment environment)
    {
        _next = next;
        _tokenService = tokenService;
        _mdb = mdb;
        _cache = cache;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (IsPublicRequest(context, path) || IsTestingContractRoute(path))
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

        var principal = _tokenService.Validate(authHeader["Bearer ".Length..].Trim());
        if (principal == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        var shopId = principal.FindFirstValue("shopId");
        var role = principal.FindFirstValue("role");
        var isAdmin = string.Equals(principal.FindFirstValue("admin"), "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userId) || !await IsActiveAsync(userId, shopId, isAdmin))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["UserId"] = userId;
        context.Items["ShopId"] = shopId;
        context.Items["Role"] = role;
        context.Items["IsPlatformAdmin"] = isAdmin;
        await _next(context);
    }

    private bool IsTestingContractRoute(string path)
        => _environment.IsEnvironment("Testing")
           && path.StartsWith("/test-contract/", StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicRequest(HttpContext context, string path)
    {
        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(path, "/public", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/public/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/plans", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/plans/", StringComparison.OrdinalIgnoreCase))
            return true;

        return HttpMethods.IsPost(context.Request.Method) && PublicPostAuthPaths.Contains(path);
    }

    private async Task<bool> IsActiveAsync(string userId, string? shopId, bool isAdmin)
    {
        var cacheKey = $"auth-status:{userId}:{shopId}:{isAdmin}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        using var db = _mdb.Open();
        var status = await db.QueryFirstOrDefaultAsync<AuthStatus>(
            """
            SELECT
                u.Status AS UserStatus,
                u.IsPlatformAdmin AS IsPlatformAdmin,
                su.Status AS ShopUserStatus,
                s.Status AS ShopStatus
            FROM User u
            LEFT JOIN ShopUser su ON su.UserId = u.Id AND (@ShopId IS NULL OR su.ShopId = @ShopId)
            LEFT JOIN Shop s ON s.Id = su.ShopId
            WHERE u.Id = @UserId
            LIMIT 1
            """,
            new { UserId = userId, ShopId = shopId });

        var active = status != null
            && string.Equals(status.UserStatus, UserStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && (isAdmin && status.IsPlatformAdmin || (
                !string.IsNullOrWhiteSpace(shopId)
                && string.Equals(status.ShopUserStatus, UserStatuses.Active, StringComparison.OrdinalIgnoreCase)
                && string.Equals(status.ShopStatus, ShopStatuses.Active, StringComparison.OrdinalIgnoreCase)));

        _cache.Set(cacheKey, active, TimeSpan.FromMinutes(5));
        return active;
    }

    private sealed class AuthStatus
    {
        public string UserStatus { get; set; } = "";
        public bool IsPlatformAdmin { get; set; }
        public string? ShopUserStatus { get; set; }
        public string? ShopStatus { get; set; }
    }
}
