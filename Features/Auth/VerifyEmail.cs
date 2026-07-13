using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Auth;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace DammaniAPI.Features.Auth;

public class VerifyEmail
{
    public class Command : IRequest<Result>
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Token { get; set; }
        public AuthUserResult? User { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
            RuleFor(x => x.Code).NotEmpty().Length(6).Matches(@"^[0-9]+$");
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly ITokenService _tokenService;
        private readonly IMemoryCache _cache;

        public CommandHandler(IManagementDatabase mdb, ITokenService tokenService, IMemoryCache cache)
        {
            _mdb = mdb;
            _tokenService = tokenService;
            _cache = cache;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var attemptKey = $"verify-email-attempts:{email}";
            if (_cache.TryGetValue(attemptKey, out int attempts) && attempts >= 10)
                return new Result { Success = false, ErrorCode = ErrorCodes.TooManyRequests };

            using var db = _mdb.Open();
            var row = await db.QueryFirstOrDefaultAsync<UserRow>(
                """
                SELECT
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.Language,
                    u.IsPlatformAdmin,
                    u.AdminRole,
                    (u.EmailVerifiedAt IS NOT NULL) AS EmailVerified,
                    su.ShopId,
                    su.Role,
                    (s.OnboardingCompletedAt IS NOT NULL) AS OnboardingCompleted
                FROM User u
                LEFT JOIN ShopUser su ON su.UserId = u.Id
                LEFT JOIN Shop s ON s.Id = su.ShopId
                WHERE LOWER(u.Email) = @Email AND u.Status = 'active'
                ORDER BY su.Role = 'owner' DESC
                LIMIT 1
                """,
                new { Email = email });

            if (row == null)
            {
                RegisterAttempt(attemptKey, attempts);
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidCode };
            }

            // An already-verified account must go through login — issuing a token
            // here would let anyone mint a session from just an email address.
            if (row.EmailVerified)
                return new Result { Success = false, ErrorCode = ErrorCodes.AlreadyVerified };

            var codeHash = RequestPasswordReset.HashToken(request.Code.Trim());
            var codeId = await db.ExecuteScalarAsync<string?>(
                """
                SELECT Id FROM EmailVerificationCode
                WHERE UserId = @UserId
                  AND CodeHash = @CodeHash
                  AND UsedAt IS NULL
                  AND ExpiresAt > UTC_TIMESTAMP()
                ORDER BY CreatedAt DESC
                LIMIT 1
                """,
                new { UserId = row.Id, CodeHash = codeHash });

            if (codeId == null)
            {
                RegisterAttempt(attemptKey, attempts);
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidCode };
            }

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    "UPDATE User SET EmailVerifiedAt = UTC_TIMESTAMP(), UpdatedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                    new { row.Id },
                    tx);
                await db.ExecuteAsync(
                    "UPDATE EmailVerificationCode SET UsedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                    new { Id = codeId },
                    tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            _cache.Remove(attemptKey);
            var user = new AuthUserResult
            {
                Id = row.Id,
                FullName = row.FullName,
                Email = row.Email,
                Phone = row.Phone,
                Language = row.Language,
                Role = row.Role,
                ShopId = row.ShopId,
                IsPlatformAdmin = row.IsPlatformAdmin,
                AdminRole = row.IsPlatformAdmin
                    ? (string.IsNullOrWhiteSpace(row.AdminRole) ? "super" : row.AdminRole)
                    : null,
                OnboardingCompleted = row.OnboardingCompleted
            };
            var token = _tokenService.Issue(new AuthUser(
                user.Id, user.FullName, user.Email, user.Language,
                user.ShopId, user.Role, user.IsPlatformAdmin, user.AdminRole));
            return new Result { Success = true, Token = token, User = user };
        }

        private void RegisterAttempt(string attemptKey, int attempts)
            => _cache.Set(attemptKey, attempts + 1, TimeSpan.FromMinutes(15));

        private sealed class UserRow
        {
            public string Id { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Email { get; set; } = "";
            public string? Phone { get; set; }
            public string Language { get; set; } = "ar";
            public bool IsPlatformAdmin { get; set; }
            public string? AdminRole { get; set; }
            public bool EmailVerified { get; set; }
            public string? ShopId { get; set; }
            public string? Role { get; set; }
            public bool OnboardingCompleted { get; set; }
        }
    }
}
