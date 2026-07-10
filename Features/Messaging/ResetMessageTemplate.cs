using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Messaging;

public class ResetMessageTemplate
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string TemplateKey { get; set; } = "";
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
            RuleFor(x => x.TemplateKey)
                .NotEmpty()
                .Must(x => DefaultMessages.Defaults.ContainsKey(x));
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

            using var db = _mdb.Open();
            await db.ExecuteAsync(
                "DELETE FROM MessageTemplate WHERE ShopId = @ShopId AND TemplateKey = @TemplateKey",
                new { request.ShopId, request.TemplateKey });

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "messages", request.TemplateKey, "messages.template_reset", request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
