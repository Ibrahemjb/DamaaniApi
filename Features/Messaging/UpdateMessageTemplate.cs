using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Messaging;

public class UpdateMessageTemplate
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string TemplateKey { get; set; } = "";
        public string? TextAr { get; set; }
        public string? TextEn { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? UnknownVariable { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.TemplateKey)
                .NotEmpty()
                .Must(x => DefaultMessages.Defaults.ContainsKey(x));
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.TextAr) || !string.IsNullOrWhiteSpace(x.TextEn))
                .WithMessage("At least one language text is required.");
            RuleFor(x => x.TextAr).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.TextAr));
            RuleFor(x => x.TextEn).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.TextEn));
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            foreach (var text in new[] { request.TextAr, request.TextEn })
            {
                var unknown = MessageTemplateVars.FindUnknown(text);
                if (unknown != null)
                    return new Result { Success = false, ErrorCode = ErrorCodes.UnknownVariable, UnknownVariable = unknown };
            }

            using var db = _mdb.Open();
            var existing = await db.ExecuteScalarAsync<string?>(
                "SELECT Id FROM MessageTemplate WHERE ShopId = @ShopId AND TemplateKey = @TemplateKey",
                new { request.ShopId, request.TemplateKey });

            if (existing != null)
            {
                await db.ExecuteAsync(
                    """
                    UPDATE MessageTemplate
                    SET TextAr = @TextAr, TextEn = @TextEn, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id
                    """,
                    new
                    {
                        Id = existing,
                        TextAr = NullIfBlank(request.TextAr),
                        TextEn = NullIfBlank(request.TextEn)
                    });
            }
            else
            {
                await db.ExecuteAsync(
                    """
                    INSERT INTO MessageTemplate (Id, ShopId, TemplateKey, TextAr, TextEn, CreatedAt)
                    VALUES (@Id, @ShopId, @TemplateKey, @TextAr, @TextEn, UTC_TIMESTAMP())
                    """,
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        request.ShopId,
                        request.TemplateKey,
                        TextAr = NullIfBlank(request.TextAr),
                        TextEn = NullIfBlank(request.TextEn)
                    });
            }

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "messages", request.TemplateKey, "messages.template_updated", request.ActorUserId);

            return new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
