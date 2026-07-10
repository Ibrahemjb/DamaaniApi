using Dapper;
using Serilog;

namespace DammaniAPI.Database;

// DMN-1101: promote an existing user to platform admin via PLATFORM_ADMIN_EMAIL.
// Never auto-create users or passwords from env.
public static class PlatformAdminProvisioner
{
    public static async Task RunAsync(IManagementDatabase mdb, string? adminEmail)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
            return;

        var email = adminEmail.Trim().ToLowerInvariant();
        using var db = mdb.Open();
        var user = await db.QueryFirstOrDefaultAsync<(string Id, bool IsPlatformAdmin)>(
            "SELECT Id, IsPlatformAdmin FROM User WHERE LOWER(Email) = @Email LIMIT 1",
            new { Email = email });

        if (user.Id is null)
        {
            Log.Information(
                "PLATFORM_ADMIN_EMAIL={Email} — no user found. Sign up normally, then restart to promote.",
                email);
            return;
        }

        if (user.IsPlatformAdmin)
        {
            Log.Information("PLATFORM_ADMIN_EMAIL={Email} — already platform admin (idempotent).", email);
            return;
        }

        await db.ExecuteAsync(
            "UPDATE User SET IsPlatformAdmin = 1, UpdatedAt = UTC_TIMESTAMP() WHERE Id = @Id",
            new { user.Id });
        Log.Information("PLATFORM_ADMIN_EMAIL={Email} — promoted user {UserId} to platform admin.", email, user.Id);
    }
}
