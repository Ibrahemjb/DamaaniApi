using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DammaniAPI.Services.Auth;

public interface ITokenService
{
    string Issue(AuthUser user);
    ClaimsPrincipal? Validate(string token);
}

public record AuthUser(
    string Id,
    string FullName,
    string Email,
    string Language,
    string? ShopId,
    string? Role,
    bool IsPlatformAdmin,
    string? AdminRole = null);

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string Issue(AuthUser user)
    {
        var secret = GetSecret();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var lifetime = int.TryParse(_configuration["JWT_LIFETIME_HOURS"], out var hours) ? hours : 72;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new("name", user.FullName),
            new("email", user.Email),
            new("lang", user.Language),
            new("admin", user.IsPlatformAdmin ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(user.ShopId))
            claims.Add(new Claim("shopId", user.ShopId));
        if (!string.IsNullOrWhiteSpace(user.Role))
            claims.Add(new Claim("role", user.Role));
        if (user.IsPlatformAdmin)
            claims.Add(new Claim("adminRole", string.IsNullOrWhiteSpace(user.AdminRole) ? "super" : user.AdminRole));

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT_ISSUER"] ?? "damaani-api",
            audience: _configuration["JWT_APP_IDENTIFIER"] ?? "dammani-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(token);
    }

    public ClaimsPrincipal? Validate(string token)
    {
        try
        {
            var secret = GetSecret();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = true,
                ValidIssuer = _configuration["JWT_ISSUER"] ?? "damaani-api",
                ValidateAudience = true,
                ValidAudience = _configuration["JWT_APP_IDENTIFIER"] ?? "dammani-api",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            return new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, parameters, out _);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JWT validation failed");
            return null;
        }
    }

    private string GetSecret()
    {
        var secret = _configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("JWT_SECRET must be set to at least 32 characters.");
        return secret;
    }
}
