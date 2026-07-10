using DammaniAPI.Features.ServiceRequests;
using Xunit;

namespace DammaniAPI.Tests;

public class ServiceRequestListFilterTests
{
    [Fact]
    public void AlwaysFiltersByShopFirst()
    {
        var (where, _) = ServiceRequestListFilter.Build("shop-1", new ServiceRequestListFilter.Args());

        Assert.StartsWith("sr.ShopId = @ShopId", where);
    }

    [Fact]
    public void SearchMatchesRequestCustomerWarrantyAndSerial()
    {
        var (where, _) = ServiceRequestListFilter.Build("shop-1", new ServiceRequestListFilter.Args
        {
            Search = "059 9123"
        });

        Assert.Contains("sr.RequestNumber LIKE @SearchPrefix", where);
        Assert.Contains("sr.CustomerName LIKE @SearchContains", where);
        Assert.Contains("w.Code LIKE @SearchPrefix", where);
        Assert.Contains("w.ProductName LIKE @SearchContains", where);
        Assert.Contains("w.SerialNumber LIKE @SearchPrefix", where);
        Assert.Contains("sr.CustomerPhone LIKE @SearchPhone", where);
    }

    [Fact]
    public void SearchWithoutDigitsSkipsPhoneClause()
    {
        var (where, _) = ServiceRequestListFilter.Build("shop-1", new ServiceRequestListFilter.Args
        {
            Search = "Ahmad"
        });

        Assert.DoesNotContain("@SearchPhone", where);
    }

    [Fact]
    public void ExpiredWarrantyStatusUsesDerivedDateLogic()
    {
        var (where, _) = ServiceRequestListFilter.Build("shop-1", new ServiceRequestListFilter.Args
        {
            WarrantyStatus = "expired"
        });

        Assert.Contains("w.Status = 'active' AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()", where);
    }

    [Fact]
    public void ActiveWarrantyStatusExcludesExpired()
    {
        var (where, _) = ServiceRequestListFilter.Build("shop-1", new ServiceRequestListFilter.Args
        {
            WarrantyStatus = "active"
        });

        Assert.Contains("w.ExpiryDate IS NULL OR w.ExpiryDate >= CURDATE()", where);
    }

    [Fact]
    public void AllFiltersCombine()
    {
        var (where, _) = ServiceRequestListFilter.Build("shop-1", new ServiceRequestListFilter.Args
        {
            Search = "x",
            Status = "new",
            DateFrom = DateTime.UtcNow.AddDays(-30),
            DateTo = DateTime.UtcNow,
            Category = "solar_battery",
            WarrantyStatus = "active",
            AssignedToUserId = "u1",
            BranchId = "b1"
        });

        Assert.Contains("sr.Status = @Status", where);
        Assert.Contains("sr.CreatedAt >= @DateFrom", where);
        Assert.Contains("sr.CreatedAt < @DateTo", where);
        Assert.Contains("w.Category = @Category", where);
        Assert.Contains("sr.AssignedToUserId = @AssignedToUserId", where);
        Assert.Contains("w.BranchId = @BranchId", where);
    }
}
