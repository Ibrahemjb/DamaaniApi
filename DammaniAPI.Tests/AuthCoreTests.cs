using System.Net;
using System.Net.Http.Headers;
using DammaniAPI.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DammaniAPI.Tests;

public class AuthCoreTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public AuthCoreTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public void PasswordHasher_RoundTrips()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", hash));
        Assert.False(hasher.Verify("wrong password", hash));
        Assert.DoesNotContain("correct horse", hash);
    }

    [Fact]
    public void TokenService_IssuesAndValidatesExpectedClaims()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "01234567890123456789012345678901",
                ["JWT_ISSUER"] = "damaani-api",
                ["JWT_APP_IDENTIFIER"] = "dammani-api",
                ["JWT_LIFETIME_HOURS"] = "72"
            })
            .Build();
        var service = new TokenService(configuration, NullLogger<TokenService>.Instance);

        var token = service.Issue(new AuthUser("u1", "Owner", "owner@example.com", "ar", "s1", "owner", false));
        var principal = service.Validate(token);

        Assert.NotNull(principal);
        Assert.Equal("s1", principal!.FindFirst("shopId")?.Value);
        Assert.Equal("owner", principal.FindFirst("role")?.Value);
        Assert.Equal("ar", principal.FindFirst("lang")?.Value);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithGarbageToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
