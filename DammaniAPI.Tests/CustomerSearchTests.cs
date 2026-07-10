using DammaniAPI.Features.Customers;
using Xunit;

namespace DammaniAPI.Tests;

public class CustomerSearchFilterTests
{
    [Fact]
    public void AlwaysFiltersByShopFirst()
    {
        var (where, _) = CustomerSearchFilter.Build("shop-1", "Ahmad");

        Assert.StartsWith("c.ShopId = @ShopId", where);
    }

    [Fact]
    public void SearchMatchesNameContains()
    {
        var (where, _) = CustomerSearchFilter.Build("shop-1", "Ahmad");

        Assert.Contains("c.Name LIKE @SearchContains", where);
    }

    [Fact]
    public void SearchWithDigitsMatchesPhone()
    {
        var (where, parameters) = CustomerSearchFilter.Build("shop-1", "0599");

        Assert.Contains("c.Phone LIKE @SearchPhone", where);
        Assert.Equal("%0599%", parameters.Get<string>("SearchPhone"));
    }

    [Fact]
    public void ArabicNameSearchUsesContains()
    {
        var (where, parameters) = CustomerSearchFilter.Build("shop-1", "أحمد");

        Assert.Contains("c.Name LIKE @SearchContains", where);
        Assert.Equal("%أحمد%", parameters.Get<string>("SearchContains"));
        Assert.DoesNotContain("@SearchPhone", where);
    }

    [Fact]
    public void MixedNameAndPhoneMatchesBothClauses()
    {
        var (where, _) = CustomerSearchFilter.Build("shop-1", "Ahmad 059");

        Assert.Contains("c.Name LIKE @SearchContains", where);
        Assert.Contains("c.Phone LIKE @SearchPhone", where);
    }
}
