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
    public const string NotFound = "not_found";
    public const string PlanLimitReached = "plan_limit_reached";
    public const string DuplicateSerial = "duplicate_serial";
    public const string ShopSuspended = "shop_suspended";
    public const string WarrantyCancelled = "warranty_cancelled";
    public const string FeatureNotInPlan = "feature_not_in_plan";
    public const string UserLimitReached = "user_limit_reached";
    public const string UnknownVariable = "unknown_variable";
    public const string DuplicateMember = "duplicate_member";
    public const string PhoneTaken = "phone_taken";
    // Public surface codes (DMN-501/504): deliberately vague — "unavailable"
    // covers suspended shops without revealing that a shop exists or why.
    public const string Unavailable = "unavailable";
    public const string NotAllowed = "not_allowed";
    public const string TooManyRequests = "too_many_requests";
    public const string InvalidFiles = "invalid_files";
    public const string RequestClosed = "request_closed";
    public const string InvalidRange = "invalid_range";
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

public static class TemplateStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}

// Stored statuses only. "Expired" is derived at read time
// (Status = 'active' AND ExpiryDate < CURDATE()) and never persisted (DMN-401).
public static class WarrantyStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Cancelled = "cancelled";
}

// BP §10.18 status set (DMN-503). Prototype omits "replaced"; the doc includes
// it, so the doc wins. "new" is the only status the public form ever writes.
public static class ServiceRequestStatuses
{
    public const string New = "new";
    public const string Reviewing = "reviewing";
    public const string WaitingCustomer = "waiting_customer";
    public const string SentSupplier = "sent_supplier";
    public const string Repaired = "repaired";
    public const string Replaced = "replaced";
    public const string Rejected = "rejected";
    public const string Closed = "closed";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        New,
        Reviewing,
        WaitingCustomer,
        SentSupplier,
        Repaired,
        Replaced,
        Rejected,
        Closed
    };
}

// BP §10.16 problem types (DMN-503).
public static class ProblemTypes
{
    public const string NotWorking = "not_working";
    public const string BatteryIssue = "battery_issue";
    public const string ChargingIssue = "charging_issue";
    public const string BrokenPart = "broken_part";
    public const string InstallationIssue = "installation_issue";
    public const string Other = "other";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        NotWorking,
        BatteryIssue,
        ChargingIssue,
        BrokenPart,
        InstallationIssue,
        Other
    };
}

// BP §10.19: closing a request requires an outcome (enforced by DMN-602).
public static class CloseOutcomes
{
    public const string Repaired = "repaired";
    public const string Replaced = "replaced";
    public const string Rejected = "rejected";
    public const string CustomerCancelled = "customer_cancelled";
    public const string Other = "other";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        Repaired,
        Replaced,
        Rejected,
        CustomerCancelled,
        Other
    };
}

public static class PreferredContacts
{
    public const string WhatsApp = "whatsapp";
    public const string Phone = "phone";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        WhatsApp,
        Phone
    };
}

public static class ServiceRequestSources
{
    public const string Public = "public";
    public const string Internal = "internal";
}

public static class PlanCodes
{
    public const string Free = "free";
    public const string Starter = "starter";
    public const string Pro = "pro";
    public const string Business = "business";
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

public static class BranchStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}
