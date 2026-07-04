using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Auth;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace DammaniAPI.Features.Auth;

public class Login
{
    public class Command : IRequest<Result>
    {
        public string Identifier { get; set; } = "";
        public string Password { get; set; } = "";
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
            RuleFor(x => x.Identifier).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IMemoryCache _cache;

        public CommandHandler(
            IManagementDatabase mdb,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IMemoryCache cache)
        {
            _mdb = mdb;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _cache = cache;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var identifier = request.Identifier.Trim();
            var lockKey = $"login-lock:{identifier.ToLowerInvariant()}";
            if (_cache.TryGetValue(lockKey, out int failures) && failures >= 5)
                return new Result { Success = false, ErrorCode = ErrorCodes.Locked };

            using var db = _mdb.Open();
            var user = await db.QueryFirstOrDefaultAsync<LoginRow>(
                """
                SELECT
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.PasswordHash,
                    u.Language,
                    u.Status AS UserStatus,
                    u.IsPlatformAdmin,
                    su.ShopId,
                    su.Role,
                    su.Status AS ShopUserStatus,
                    s.Status AS ShopStatus
                FROM User u
                LEFT JOIN ShopUser su ON su.UserId = u.Id
                LEFT JOIN Shop s ON s.Id = su.ShopId
                WHERE (@IsEmail = 1 AND LOWER(u.Email) = @Identifier)
                   OR (@IsEmail = 0 AND u.Phone = @Identifier)
                ORDER BY su.Role = 'owner' DESC
                LIMIT 1
                """,
                new
                {
                    IsEmail = identifier.Contains('@') ? 1 : 0,
                    Identifier = identifier.Contains('@') ? identifier.ToLowerInvariant() : identifier
                });

            if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash ?? "") || !IsLoginActive(user))
            {
                RegisterFailure(lockKey, failures);
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidCredentials };
            }

            _cache.Remove(lockKey);
            var resultUser = new AuthUserResult
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Language = user.Language,
                Role = user.Role,
                ShopId = user.ShopId,
                IsPlatformAdmin = user.IsPlatformAdmin
            };
            var token = _tokenService.Issue(new AuthUser(resultUser.Id, resultUser.FullName, resultUser.Email, resultUser.Language, resultUser.ShopId, resultUser.Role, resultUser.IsPlatformAdmin));
            return new Result { Success = true, Token = token, User = resultUser };
        }

        private void RegisterFailure(string lockKey, int failures)
            => _cache.Set(lockKey, failures + 1, TimeSpan.FromSeconds(failures + 1 >= 5 ? 60 : 300));

        private static bool IsLoginActive(LoginRow user)
            => string.Equals(user.UserStatus, UserStatuses.Active, StringComparison.OrdinalIgnoreCase)
               && (user.IsPlatformAdmin || (
                   !string.IsNullOrWhiteSpace(user.ShopId)
                   && string.Equals(user.ShopUserStatus, UserStatuses.Active, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(user.ShopStatus, ShopStatuses.Active, StringComparison.OrdinalIgnoreCase)));

        private sealed class LoginRow
        {
            public string Id { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Email { get; set; } = "";
            public string? Phone { get; set; }
            public string? PasswordHash { get; set; }
            public string Language { get; set; } = "ar";
            public string UserStatus { get; set; } = "";
            public bool IsPlatformAdmin { get; set; }
            public string? ShopId { get; set; }
            public string? Role { get; set; }
            public string? ShopUserStatus { get; set; }
            public string? ShopStatus { get; set; }
        }
    }
}
