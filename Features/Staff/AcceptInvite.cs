using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Auth;
using DammaniAPI.Services.Auth;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class AcceptInvite
{
    public class Command : IRequest<Result>
    {
        public string Token { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Phone { get; set; }
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
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Password).MinimumLength(8);
            RuleFor(x => x.Phone).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.Phone));
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
            var tokenHash = RequestPasswordReset.HashToken(request.Token.Trim());
            using var db = _mdb.Open();
            var invite = await db.QueryFirstOrDefaultAsync<InviteRow>(
                """
                SELECT i.Id, i.ShopId, i.Email, i.Phone, i.ExpiresAt, i.AcceptedAt,
                       s.Name AS ShopName, (s.OnboardingCompletedAt IS NOT NULL) AS OnboardingCompleted
                FROM StaffInvite i
                JOIN Shop s ON s.Id = i.ShopId
                WHERE i.TokenHash = @TokenHash
                """,
                new { TokenHash = tokenHash });

            if (invite == null || invite.AcceptedAt != null || invite.ExpiresAt <= DateTime.UtcNow)
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidOrExpiredToken };

            if (!await InviteStaff.CommandHandler.HasCapacityAsync(db, invite.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.UserLimitReached };

            using var tx = db.BeginTransaction();
            try
            {
                string userId;
                string? email = invite.Email;
                string? phone = NullIfBlank(request.Phone) ?? invite.Phone;
                var existingUserId = email != null
                    ? await db.ExecuteScalarAsync<string?>(
                        "SELECT Id FROM User WHERE LOWER(Email) = @Email",
                        new { Email = email }, tx)
                    : null;

                if (existingUserId != null)
                {
                    userId = existingUserId;
                    await db.ExecuteAsync(
                        "UPDATE User SET FullName = @FullName, Phone = COALESCE(@Phone, Phone), UpdatedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                        new { Id = userId, FullName = request.FullName.Trim(), Phone = phone }, tx);
                }
                else
                {
                    if (email == null)
                        return new Result { Success = false, ErrorCode = ErrorCodes.InvalidOrExpiredToken };

                    userId = Guid.NewGuid().ToString();
                    await db.ExecuteAsync(
                        """
                        INSERT INTO User (Id, Email, FullName, Phone, PasswordHash, Language, Status, IsPlatformAdmin, EmailVerifiedAt, CreatedAt)
                        VALUES (@Id, @Email, @FullName, @Phone, @PasswordHash, @Language, 'active', 0, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                        """,
                        new
                        {
                            Id = userId,
                            Email = email,
                            FullName = request.FullName.Trim(),
                            Phone = phone,
                            PasswordHash = _passwordHasher.Hash(request.Password),
                            Language = Languages.Arabic
                        },
                        tx);
                }

                var shopUserExists = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM ShopUser WHERE ShopId = @ShopId AND UserId = @UserId",
                    new { invite.ShopId, UserId = userId }, tx);
                if (shopUserExists == 0)
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO ShopUser (Id, ShopId, UserId, Role, Status, CreatedAt)
                        VALUES (@Id, @ShopId, @UserId, @Role, 'active', UTC_TIMESTAMP())
                        """,
                        new
                        {
                            Id = Guid.NewGuid().ToString(),
                            invite.ShopId,
                            UserId = userId,
                            Role = Roles.Staff
                        },
                        tx);
                }

                await db.ExecuteAsync(
                    "UPDATE StaffInvite SET AcceptedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                    new { invite.Id }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, invite.ShopId, "user", userId, "staff.joined", userId);

                tx.Commit();

                var authUser = new AuthUserResult
                {
                    Id = userId,
                    FullName = request.FullName.Trim(),
                    Email = email ?? "",
                    Phone = phone,
                    Language = Languages.Arabic,
                    Role = Roles.Staff,
                    ShopId = invite.ShopId,
                    OnboardingCompleted = invite.OnboardingCompleted
                };
                var jwt = _tokenService.Issue(new AuthUser(
                    authUser.Id, authUser.FullName, authUser.Email, authUser.Language,
                    authUser.ShopId, authUser.Role, false));

                return new Result { Success = true, Token = jwt, User = authUser };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private sealed class InviteRow
        {
            public string Id { get; set; } = "";
            public string ShopId { get; set; } = "";
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime? AcceptedAt { get; set; }
            public string ShopName { get; set; } = "";
            public bool OnboardingCompleted { get; set; }
        }
    }
}
