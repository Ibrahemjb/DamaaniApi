using DammaniAPI.Features.Admin;
using DammaniAPI.Features.Billing;
using DammaniAPI.Features.Dashboard;
using DammaniAPI.Features.Messaging;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DammaniAPI.Tests;

public class AdminPlanRulesTests
{
    [Fact]
    public void IsUpgradeRequest_RequiresHigherSortOrder()
    {
        var current = new BillingPlan { SortOrder = 1 };
        var upgrade = new BillingPlan { SortOrder = 3 };
        var downgrade = new BillingPlan { SortOrder = 1 };

        Assert.True(AdminPlanRules.IsUpgradeRequest(current, upgrade));
        Assert.False(AdminPlanRules.IsUpgradeRequest(current, downgrade));
        Assert.False(AdminPlanRules.IsUpgradeRequest(current, current));
    }

    [Fact]
    public void ExtractReference_ParsesActivityJson()
    {
        const string json = "{\"planCode\":\"pro\",\"reference\":\"UP-ABC123\"}";
        Assert.Equal("UP-ABC123", GetPlanOverview.QueryHandler.ExtractReference(json));
    }
}

public class MessageResolverOrderTests
{
    [Fact]
    public void DefaultMessages_ContainsCoreKeys()
    {
        Assert.True(DefaultMessages.Defaults.ContainsKey(DefaultMessages.Keys.StatusRepaired));
        Assert.True(DefaultMessages.Defaults.ContainsKey(DefaultMessages.Keys.WarrantyCreated));
    }
}

public class AuthorizeAdminFilterTests
{
    [Theory]
    [InlineData(false, "user-1", true)]
    [InlineData(true, "admin-1", false)]
    public async Task AdminEndpoint_RequiresPlatformAdmin(bool isAdmin, string userId, bool expectForbidden)
    {
        var filter = new AuthorizeAdminFilter();
        var context = CreateContext(isAdmin, userId);
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
        });

        if (expectForbidden)
        {
            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
            Assert.False(executed);
        }
        else
        {
            Assert.Null(context.Result);
            Assert.True(executed);
        }
    }

    [Fact]
    public void TokenService_AdminClaim_IsSetForPlatformAdmin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "01234567890123456789012345678901",
                ["JWT_ISSUER"] = "damaani-api",
                ["JWT_APP_IDENTIFIER"] = "dammani-api"
            })
            .Build();
        var service = new TokenService(configuration, NullLogger<TokenService>.Instance);
        var token = service.Issue(new AuthUser("admin-1", "Admin", "admin@example.com", "en", null, null, true, AdminRoles.Super));
        var principal = service.Validate(token);

        Assert.Equal("true", principal!.FindFirst("admin")?.Value);
        Assert.Equal("super", principal.FindFirst("adminRole")?.Value);
        Assert.Null(principal.FindFirst("shopId")?.Value);
    }

    [Fact]
    public async Task AdminEndpoint_RoleGate_BlocksBillingOnlyOnSuspend()
    {
        var filter = new AuthorizeAdminFilter(new[] { AdminRoles.Super, AdminRoles.Support });
        var context = CreateContext(true, "billing-1", adminRole: AdminRoles.Billing);
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
        });

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.False(executed);
    }

    [Fact]
    public async Task AdminEndpoint_RoleGate_AllowsSupportOnSuspend()
    {
        var filter = new AuthorizeAdminFilter(new[] { AdminRoles.Super, AdminRoles.Support });
        var context = CreateContext(true, "support-1", adminRole: AdminRoles.Support);
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
        });

        Assert.Null(context.Result);
        Assert.True(executed);
    }

    [Fact]
    public async Task ShopEndpoint_RejectsAdminWithoutShopRole()
    {
        var filter = new AuthorizeFilter([Features.Roles.Staff]);
        var context = CreateContext(true, "admin-1", role: null, shopId: null);
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
        });

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.False(executed);
    }

    [Fact]
    public async Task ShopEndpoint_AllowsOwnerWithShop()
    {
        var filter = new AuthorizeFilter([Features.Roles.Staff]);
        var context = CreateContext(false, "owner-1", role: Features.Roles.Owner, shopId: "shop-1");
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
        });

        Assert.Null(context.Result);
        Assert.True(executed);
    }

    private static ActionExecutingContext CreateContext(
        bool isAdmin,
        string userId,
        string? role = null,
        string? shopId = null,
        string? adminRole = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;
        httpContext.Items["IsPlatformAdmin"] = isAdmin;
        if (role is not null) httpContext.Items["Role"] = role;
        if (shopId is not null) httpContext.Items["ShopId"] = shopId;
        if (isAdmin) httpContext.Items["AdminRole"] = adminRole ?? AdminRoles.Super;

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            null!);
    }
}

public class GetSummaryTenancyTests
{
    [Fact]
    public async Task GetSummary_WithoutShopId_ReturnsUnauthorized()
    {
        var handler = new GetSummary.QueryHandler(new ThrowingDatabase());
        var result = await handler.Handle(new GetSummary.Query(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(Features.ErrorCodes.Unauthorized, result.ErrorCode);
    }

    private sealed class ThrowingDatabase : Database.IManagementDatabase
    {
        public System.Data.IDbConnection Open() => throw new InvalidOperationException("should not open");
    }
}
