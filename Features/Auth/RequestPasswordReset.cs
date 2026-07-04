using System.Security.Cryptography;
using System.Text;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Services.Email;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace DammaniAPI.Features.Auth;

public class RequestPasswordReset
{
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public class Command : IRequest<Result>
    {
        public string Email { get; set; } = "";
        public string? IpAddress { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public CommandHandler(
            IManagementDatabase mdb,
            IEmailSender emailSender,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _mdb = mdb;
            _emailSender = emailSender;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var rateKey = $"reset-request:{email}:{request.IpAddress}";
            var count = _cache.GetOrCreate(rateKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                return 0;
            });
            if (count >= 3)
                return new Result { Success = true };
            _cache.Set(rateKey, count + 1, TimeSpan.FromMinutes(15));

            using var db = _mdb.Open();
            var user = await db.QueryFirstOrDefaultAsync<UserRow>(
                "SELECT Id, Email, Language FROM User WHERE LOWER(Email) = @Email AND Status = 'active'",
                new { Email = email });

            if (user == null)
            {
                await Task.Delay(250, ct);
                return new Result { Success = true };
            }

            var token = CreateToken();
            var tokenHash = HashToken(token);
            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    "UPDATE PasswordResetToken SET UsedAt = UTC_TIMESTAMP() WHERE UserId = @UserId AND UsedAt IS NULL",
                    new { UserId = user.Id },
                    tx);
                await db.ExecuteAsync(
                    """
                    INSERT INTO PasswordResetToken (Id, UserId, TokenHash, ExpiresAt, CreatedAt)
                    VALUES (@Id, @UserId, @TokenHash, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 60 MINUTE), UTC_TIMESTAMP())
                    """,
                    new { Id = Guid.NewGuid().ToString(), UserId = user.Id, TokenHash = tokenHash },
                    tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            var baseUrl = (_configuration["APP_BASE_URL"] ?? "http://localhost:5173").TrimEnd('/');
            var resetUrl = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
            await _emailSender.SendPasswordResetAsync(user.Email, user.Language, resetUrl, ct);
            return new Result { Success = true };
        }

        private static string CreateToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private sealed class UserRow
        {
            public string Id { get; set; } = "";
            public string Email { get; set; } = "";
            public string Language { get; set; } = "ar";
        }
    }
}
