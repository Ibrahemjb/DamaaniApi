using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Auth;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Auth;

public class ResetPassword
{
    public class Command : IRequest<Result>
    {
        public string Token { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.NewPassword).MinimumLength(8);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;
        private readonly IPasswordHasher _passwordHasher;

        public CommandHandler(IManagementDatabase mdb, IPasswordHasher passwordHasher)
        {
            _mdb = mdb;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var tokenHash = RequestPasswordReset.HashToken(request.Token);
            var reset = await db.QueryFirstOrDefaultAsync<ResetRow>(
                """
                SELECT prt.Id, prt.UserId, su.ShopId
                FROM PasswordResetToken prt
                LEFT JOIN ShopUser su ON su.UserId = prt.UserId
                WHERE prt.TokenHash = @TokenHash
                  AND prt.UsedAt IS NULL
                  AND prt.ExpiresAt > UTC_TIMESTAMP()
                ORDER BY prt.CreatedAt DESC
                LIMIT 1
                """,
                new { TokenHash = tokenHash });

            if (reset == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidOrExpiredToken };

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    "UPDATE User SET PasswordHash = @PasswordHash, UpdatedAt = UTC_TIMESTAMP() WHERE Id = @UserId",
                    new { PasswordHash = _passwordHasher.Hash(request.NewPassword), reset.UserId },
                    tx);
                await db.ExecuteAsync(
                    "UPDATE PasswordResetToken SET UsedAt = UTC_TIMESTAMP() WHERE Id = @Id",
                    new { reset.Id },
                    tx);
                await ActivityLogger.LogAsync(db, tx, reset.ShopId, "user", reset.UserId, "user.password_reset", reset.UserId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true };
        }

        private sealed class ResetRow
        {
            public string Id { get; set; } = "";
            public string UserId { get; set; } = "";
            public string? ShopId { get; set; }
        }
    }
}
