using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Services.Email;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace DammaniAPI.Features.Auth;

public class ResendVerification
{
    public class Command : IRequest<Result>
    {
        public string Email { get; set; } = "";
        public string? IpAddress { get; set; }
    }

    // Always reports success so the endpoint cannot be used to probe which
    // emails are registered (same posture as RequestPasswordReset).
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
        private readonly IMemoryCache _cache;

        public CommandHandler(IManagementDatabase mdb, IEmailSender emailSender, IMemoryCache cache)
        {
            _mdb = mdb;
            _emailSender = emailSender;
            _cache = cache;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var rateKey = $"verify-resend:{email}:{request.IpAddress}";
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
                """
                SELECT Id, Email, Language FROM User
                WHERE LOWER(Email) = @Email AND Status = 'active' AND EmailVerifiedAt IS NULL
                """,
                new { Email = email });

            if (user == null)
            {
                await Task.Delay(250, ct);
                return new Result { Success = true };
            }

            var code = await EmailVerificationCodes.IssueAsync(db, null, user.Id);
            await _emailSender.SendEmailVerificationAsync(user.Email, user.Language, code, ct);
            return new Result { Success = true };
        }

        private sealed class UserRow
        {
            public string Id { get; set; } = "";
            public string Email { get; set; } = "";
            public string Language { get; set; } = "ar";
        }
    }
}
