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

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAdminAttribute : TypeFilterAttribute
{
    public AuthorizeAdminAttribute() : base(typeof(AuthorizeAdminFilter))
    {
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
        var isAdmin = context.HttpContext.Items["IsPlatformAdmin"] as bool? == true;
        var allowed = _roles.Any(required => RoleAllows(role, required)) || isAdmin;
        if (!allowed)
        {
            context.Result = new ForbidResult();
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
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isAdmin = context.HttpContext.Items["IsPlatformAdmin"] as bool? == true;
        if (!isAdmin)
        {
            context.Result = string.IsNullOrWhiteSpace(context.HttpContext.Items["UserId"] as string)
                ? new UnauthorizedResult()
                : new ForbidResult();
            return;
        }

        await next();
    }
}
