using System.Security.Cryptography;
using System.Text;
using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Auth;
using DammaniAPI.Services.Email;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Staff;

public class InviteStaff
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? InviteId { get; set; }
        public string? InviteUrl { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x)
                .Must(x => HasEmailXorPhone(x.Email, x.Phone))
                .WithMessage("Email or phone is required, not both.");
            RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Email));
            RuleFor(x => x.Phone).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.Phone));
        }

        internal static bool HasEmailXorPhone(string? email, string? phone)
        {
            var hasEmail = !string.IsNullOrWhiteSpace(email);
            var hasPhone = !string.IsNullOrWhiteSpace(phone);
            return hasEmail ^ hasPhone;
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;

        public CommandHandler(IManagementDatabase mdb, IEmailSender emailSender, IConfiguration configuration)
        {
            _mdb = mdb;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ActorUserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
            var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

            using var db = _mdb.Open();

            if (!await HasCapacityAsync(db, request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.UserLimitReached };

            if (await IsDuplicateAsync(db, request.ShopId, email, phone))
                return new Result { Success = false, ErrorCode = ErrorCodes.DuplicateMember };

            var shopName = await db.ExecuteScalarAsync<string?>(
                "SELECT Name FROM Shop WHERE Id = @ShopId", new { request.ShopId }) ?? "Shop";

            var token = CreateToken();
            var tokenHash = RequestPasswordReset.HashToken(token);
            var inviteId = Guid.NewGuid().ToString();
            await db.ExecuteAsync(
                """
                INSERT INTO StaffInvite (Id, ShopId, Email, Phone, Role, TokenHash, ExpiresAt, CreatedByUserId, CreatedAt)
                VALUES (@Id, @ShopId, @Email, @Phone, @Role, @TokenHash, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 7 DAY), @CreatedByUserId, UTC_TIMESTAMP())
                """,
                new
                {
                    Id = inviteId,
                    request.ShopId,
                    Email = email,
                    Phone = phone,
                    Role = Roles.Staff,
                    TokenHash = tokenHash,
                    CreatedByUserId = request.ActorUserId
                });

            var baseUrl = (_configuration["APP_BASE_URL"] ?? "http://localhost:5173").TrimEnd('/');
            var inviteUrl = $"{baseUrl}/invite/{Uri.EscapeDataString(token)}";

            if (email != null)
            {
                var ownerLang = await db.ExecuteScalarAsync<string>(
                    "SELECT Language FROM User WHERE Id = @Id", new { Id = request.ActorUserId }) ?? Languages.Arabic;
                await _emailSender.SendStaffInviteAsync(email, ownerLang, shopName, inviteUrl, ct);
            }

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "staff_invite", inviteId, "staff.invited", request.ActorUserId);

            return new Result { Success = true, InviteId = inviteId, InviteUrl = inviteUrl };
        }

        internal static async Task<bool> HasCapacityAsync(System.Data.IDbConnection db, string shopId)
        {
            var maxUsers = await db.ExecuteScalarAsync<int?>(
                """
                SELECT p.MaxUsers FROM Subscription sub
                JOIN Plan p ON p.Id = sub.PlanId WHERE sub.ShopId = @ShopId
                """,
                new { ShopId = shopId }) ?? 1;

            var used = await db.ExecuteScalarAsync<int>(
                """
                SELECT
                  (SELECT COUNT(*) FROM ShopUser WHERE ShopId = @ShopId AND Status = @Active) +
                  (SELECT COUNT(*) FROM StaffInvite WHERE ShopId = @ShopId AND AcceptedAt IS NULL AND ExpiresAt > UTC_TIMESTAMP())
                """,
                new { ShopId = shopId, Active = UserStatuses.Active });

            return used < maxUsers;
        }

        private static async Task<bool> IsDuplicateAsync(
            System.Data.IDbConnection db, string shopId, string? email, string? phone)
        {
            if (email != null)
            {
                var member = await db.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*) FROM ShopUser su
                    JOIN User u ON u.Id = su.UserId
                    WHERE su.ShopId = @ShopId AND LOWER(u.Email) = @Email
                    """,
                    new { ShopId = shopId, Email = email });
                if (member > 0) return true;

                var invite = await db.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*) FROM StaffInvite
                    WHERE ShopId = @ShopId AND LOWER(Email) = @Email AND AcceptedAt IS NULL AND ExpiresAt > UTC_TIMESTAMP()
                    """,
                    new { ShopId = shopId, Email = email });
                if (invite > 0) return true;
            }

            if (phone != null)
            {
                var member = await db.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*) FROM ShopUser su
                    JOIN User u ON u.Id = su.UserId
                    WHERE su.ShopId = @ShopId AND u.Phone = @Phone
                    """,
                    new { ShopId = shopId, Phone = phone });
                if (member > 0) return true;

                var invite = await db.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*) FROM StaffInvite
                    WHERE ShopId = @ShopId AND Phone = @Phone AND AcceptedAt IS NULL AND ExpiresAt > UTC_TIMESTAMP()
                    """,
                    new { ShopId = shopId, Phone = phone });
                if (invite > 0) return true;
            }

            return false;
        }

        private static string CreateToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
