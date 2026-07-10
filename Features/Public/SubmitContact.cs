using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Services.Email;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace DammaniAPI.Features.Public;

public class SubmitContact
{
    public class Command : IRequest<Result>
    {
        public string? Name { get; set; }
        public string Email { get; set; } = "";
        public string? Topic { get; set; }
        public string Message { get; set; } = "";
        public string? Website { get; set; }

        // Honeypot — must stay empty. Bound from JSON/form; bots fill it.
        public string? ClientIp { get; set; }
        public string? ShopId { get; set; }
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
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
            RuleFor(x => x.Name).MaximumLength(120);
            RuleFor(x => x.Topic).MaximumLength(40);
            RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(4000);
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        internal const int RateLimitMax = 3;
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

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
            if (!string.IsNullOrWhiteSpace(request.Website))
                return new Result { Success = true };

            var rateKey = $"contact-rate:{request.ClientIp}";
            var count = _cache.GetOrCreate(rateKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = RateLimitWindow;
                return 0;
            });
            if (count >= RateLimitMax)
                return new Result { Success = false, ErrorCode = ErrorCodes.TooManyRequests };

            var id = Guid.NewGuid().ToString();
            using var db = _mdb.Open();
            await db.ExecuteAsync(
                """
                INSERT INTO ContactMessage (Id, Name, Email, Topic, Message, ShopId, CreatedAt)
                VALUES (@Id, @Name, @Email, @Topic, @Message, @ShopId, UTC_TIMESTAMP())
                """,
                new
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
                    Email = request.Email.Trim().ToLowerInvariant(),
                    Topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim(),
                    Message = request.Message.Trim(),
                    ShopId = request.ShopId
                });

            var inbox = _configuration["CONTACT_INBOX_EMAIL"] ?? "support@damaani.ps";
            await _emailSender.SendContactMessageAsync(
                inbox,
                request.Email.Trim(),
                request.Name,
                request.Topic,
                request.Message.Trim(),
                ct);

            _cache.Set(rateKey, count + 1, RateLimitWindow);
            return new Result { Success = true };
        }
    }
}
