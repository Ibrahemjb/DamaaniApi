using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Auth;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Users;

public class ChangePassword
{
    public class Command : IRequest<Result>
    {
        public string? UserId { get; set; }
        public string CurrentPassword { get; set; } = "";
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
            RuleFor(x => x.CurrentPassword).NotEmpty();
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
            if (string.IsNullOrWhiteSpace(request.UserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            var row = await db.QueryFirstOrDefaultAsync<UserRow>(
                """
                SELECT u.PasswordHash, su.ShopId
                FROM User u
                LEFT JOIN ShopUser su ON su.UserId = u.Id
                WHERE u.Id = @UserId
                LIMIT 1
                """,
                new { request.UserId });

            if (row == null || !_passwordHasher.Verify(request.CurrentPassword, row.PasswordHash ?? ""))
                return new Result { Success = false, ErrorCode = ErrorCodes.WrongPassword };

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    "UPDATE User SET PasswordHash = @PasswordHash, UpdatedAt = UTC_TIMESTAMP() WHERE Id = @UserId",
                    new { PasswordHash = _passwordHasher.Hash(request.NewPassword), request.UserId },
                    tx);
                await ActivityLogger.LogAsync(db, tx, row.ShopId, "user", request.UserId, "user.password_changed", request.UserId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true };
        }

        private sealed class UserRow
        {
            public string? PasswordHash { get; set; }
            public string? ShopId { get; set; }
        }
    }
}
