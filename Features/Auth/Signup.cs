using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Auth;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Auth;

public class Signup
{
    public class Command : IRequest<Result>
    {
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Language { get; set; } = Languages.Arabic;
        public string Country { get; set; } = "PS";
        public string ShopName { get; set; } = "";
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
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
            RuleFor(x => x.Password).MinimumLength(8);
            RuleFor(x => x.Phone).NotEmpty().MaximumLength(32).Matches(@"^[0-9+\-\s()]+$");
            RuleFor(x => x.Language).Must(x => Languages.Supported.Contains(x));
            RuleFor(x => x.Country).NotEmpty().Length(2);
            RuleFor(x => x.ShopName).NotEmpty().MaximumLength(160);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;

        public CommandHandler(IManagementDatabase mdb, IPasswordHasher passwordHasher, ITokenService tokenService)
        {
            _mdb = mdb;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            using var db = _mdb.Open();

            var existing = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM User WHERE LOWER(Email) = @Email",
                new { Email = email });
            if (existing > 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.EmailTaken };

            var userId = Guid.NewGuid().ToString();
            var shopId = Guid.NewGuid().ToString();
            var shopUserId = Guid.NewGuid().ToString();
            var subscriptionId = Guid.NewGuid().ToString();
            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    INSERT INTO User (Id, Email, FullName, Phone, PasswordHash, Language, Status, IsPlatformAdmin, CreatedAt)
                    VALUES (@Id, @Email, @FullName, @Phone, @PasswordHash, @Language, 'active', 0, UTC_TIMESTAMP())
                    """,
                    new
                    {
                        Id = userId,
                        Email = email,
                        FullName = request.FullName.Trim(),
                        Phone = request.Phone.Trim(),
                        PasswordHash = _passwordHasher.Hash(request.Password),
                        request.Language
                    },
                    tx);

                await db.ExecuteAsync(
                    """
                    INSERT INTO Shop (Id, Name, Phone, Country, Status, CreatedAt)
                    VALUES (@Id, @Name, @Phone, @Country, 'active', UTC_TIMESTAMP())
                    """,
                    new { Id = shopId, Name = request.ShopName.Trim(), Phone = request.Phone.Trim(), request.Country },
                    tx);

                await db.ExecuteAsync(
                    """
                    INSERT INTO ShopUser (Id, ShopId, UserId, Role, Status, CreatedAt)
                    VALUES (@Id, @ShopId, @UserId, @Role, 'active', UTC_TIMESTAMP())
                    """,
                    new { Id = shopUserId, ShopId = shopId, UserId = userId, Role = Roles.Owner },
                    tx);

                // DMN-1001: every shop always has an active subscription; new shops start on Free.
                var freePlanId = await db.ExecuteScalarAsync<string?>(
                    "SELECT Id FROM Plan WHERE Code = @Code AND IsActive = 1",
                    new { Code = PlanCodes.Free },
                    tx) ?? throw new InvalidOperationException("Free plan seed is missing — migrations have not been applied.");

                await db.ExecuteAsync(
                    """
                    INSERT INTO Subscription (Id, ShopId, PlanId, Status, CurrentPeriodStart, CurrentPeriodEnd, CreatedAt)
                    VALUES (@Id, @ShopId, @PlanId, 'active', DATE_FORMAT(UTC_DATE(), '%Y-%m-01'), LAST_DAY(UTC_DATE()), UTC_TIMESTAMP())
                    """,
                    new { Id = subscriptionId, ShopId = shopId, PlanId = freePlanId },
                    tx);

                await ActivityLogger.LogAsync(db, tx, shopId, "shop", shopId, "shop.created", userId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            var user = new AuthUserResult
            {
                Id = userId,
                FullName = request.FullName.Trim(),
                Email = email,
                Phone = request.Phone.Trim(),
                Language = request.Language,
                Role = Roles.Owner,
                ShopId = shopId,
                IsPlatformAdmin = false,
                OnboardingCompleted = false
            };

            var token = _tokenService.Issue(new AuthUser(user.Id, user.FullName, user.Email, user.Language, user.ShopId, user.Role, user.IsPlatformAdmin));
            return new Result { Success = true, Token = token, User = user };
        }
    }
}
