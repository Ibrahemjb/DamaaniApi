namespace DammaniAPI.Features;

public static class ErrorCodes
{
    public const string InternalError = "internal_error";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Locked = "temporarily_locked";
    public const string EmailTaken = "email_taken";
    public const string InvalidOrExpiredToken = "invalid_or_expired_token";
    public const string WrongPassword = "wrong_password";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
}

public static class Roles
{
    public const string Owner = "owner";
    public const string Staff = "staff";
}

public static class UserStatuses
{
    public const string Active = "active";
    public const string Disabled = "disabled";
}

public static class ShopStatuses
{
    public const string Active = "active";
    public const string Suspended = "suspended";
}

public static class Languages
{
    public const string Arabic = "ar";
    public const string English = "en";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        Arabic,
        English
    };
}

public static class BusinessCategories
{
    public const string SolarBattery = "solar_battery";
    public const string MobileElectronics = "mobile_electronics";
    public const string Appliances = "appliances";
    public const string FurnitureTools = "furniture_tools";
    public const string Other = "other";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        SolarBattery,
        MobileElectronics,
        Appliances,
        FurnitureTools,
        Other
    };
}
