using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Users;

public class UpdateProfile
{
    public class Command : IRequest<Result>
    {
        public string? UserId { get; set; }
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string Language { get; set; } = Languages.Arabic;
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
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Phone).MaximumLength(32).Matches(@"^[0-9+\-\s()]+$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
            RuleFor(x => x.Language).Must(x => Languages.Supported.Contains(x));
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            await db.ExecuteAsync(
                """
                UPDATE User
                SET FullName = @FullName, Phone = @Phone, Language = @Language, UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @UserId
                """,
                new
                {
                    UserId = request.UserId,
                    FullName = request.FullName.Trim(),
                    Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                    request.Language
                });

            return new Result { Success = true };
        }
    }
}
