using System.Data;
using System.Security.Cryptography;
using Dapper;

namespace DammaniAPI.Features.Auth;

// Signup and ResendVerification both issue codes; VerifyEmail consumes them.
// Codes are stored hashed (same scheme as PasswordResetToken) and short-lived.
public static class EmailVerificationCodes
{
    public const int ExpiryMinutes = 15;

    public static async Task<string> IssueAsync(IDbConnection db, IDbTransaction? tx, string userId)
    {
        var code = GenerateCode();
        await db.ExecuteAsync(
            "UPDATE EmailVerificationCode SET UsedAt = UTC_TIMESTAMP() WHERE UserId = @UserId AND UsedAt IS NULL",
            new { UserId = userId },
            tx);
        await db.ExecuteAsync(
            """
            INSERT INTO EmailVerificationCode (Id, UserId, CodeHash, ExpiresAt, CreatedAt)
            VALUES (@Id, @UserId, @CodeHash, DATE_ADD(UTC_TIMESTAMP(), INTERVAL @ExpiryMinutes MINUTE), UTC_TIMESTAMP())
            """,
            new { Id = Guid.NewGuid().ToString(), UserId = userId, CodeHash = RequestPasswordReset.HashToken(code), ExpiryMinutes },
            tx);
        return code;
    }

    private static string GenerateCode()
    {
#if DEBUG
        return "123456";
#endif
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }
}
