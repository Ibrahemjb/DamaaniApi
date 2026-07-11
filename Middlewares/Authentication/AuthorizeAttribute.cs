using DammaniAPI.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DammaniAPI.Middlewares.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAttribute : TypeFilterAttribute
{
    public AuthorizeAttribute(params string[] roles) : base(typeof(AuthorizeFilter))
    {
        Arguments = new object[] { roles };
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizeAdminAttribute : TypeFilterAttribute
{
    public AuthorizeAdminAttribute(params string[] adminRoles) : base(typeof(AuthorizeAdminFilter))
    {
        Arguments = new object[] { adminRoles };
    }
}

public class AuthorizeFilter : IAsyncActionFilter
{
    private readonly string[] _roles;

    public AuthorizeFilter(string[] roles) => _roles = roles;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.Items["UserId"] as string;
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (_roles.Length == 0)
        {
            await next();
            return;
        }

        var role = context.HttpContext.Items["Role"] as string;
        // DMN-1101: platform admins use /admin/* only; no implicit shop access without ShopUser role.
        var allowed = _roles.Any(required => RoleAllows(role, required));
        if (!allowed)
        {
            // ponytail: StatusCodeResult — ForbidResult needs AddAuthentication schemes we don't use
            context.Result = AuthResults.Forbidden();
            return;
        }

        await next();
    }

    private static bool RoleAllows(string? actual, string required)
    {
        if (string.Equals(actual, required, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(actual, Roles.Owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(required, Roles.Staff, StringComparison.OrdinalIgnoreCase);
    }
}

public class AuthorizeAdminFilter : IAsyncActionFilter
{
    private readonly string[] _adminRoles;

    public AuthorizeAdminFilter() : this(Array.Empty<string>())
    {
    }

    public AuthorizeAdminFilter(string[] adminRoles) => _adminRoles = adminRoles ?? Array.Empty<string>();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isAdmin = context.HttpContext.Items["IsPlatformAdmin"] as bool? == true;
        if (!isAdmin)
        {
            context.Result = string.IsNullOrWhiteSpace(context.HttpContext.Items["UserId"] as string)
                ? new UnauthorizedResult()
                : AuthResults.Forbidden();
            return;
        }

        if (_adminRoles.Length > 0)
        {
            var role = context.HttpContext.Items["AdminRole"] as string;
            if (!DammaniAPI.Features.Admin.AdminRoles.Allows(role, _adminRoles))
            {
                context.Result = AuthResults.Forbidden();
                return;
            }
        }

        await next();
    }
}

// Shared 403 without ASP.NET Identity schemes (custom JWT middleware only).
public static class AuthResults
{
    public static IActionResult Forbidden() => new ObjectResult(new
    {
        success = false,
        errorCode = ErrorCodes.Forbidden
    })
    {
        StatusCode = StatusCodes.Status403Forbidden
    };
}
