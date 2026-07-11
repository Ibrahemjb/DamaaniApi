namespace DammaniAPI.Features.Auth;

public class AuthUserResult
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Language { get; set; } = "ar";
    public string? Role { get; set; }
    public string? ShopId { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public string? AdminRole { get; set; }
    public bool OnboardingCompleted { get; set; }
}
