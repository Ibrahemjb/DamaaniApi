using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Features.Messaging;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Admin;

public class GetPlatformMessages
{
    public class Query : IRequest<Result> { }

    public class MessageRow
    {
        public string Key { get; set; } = "";
        public string Ar { get; set; } = "";
        public string En { get; set; } = "";
        public string DefaultAr { get; set; } = "";
        public string DefaultEn { get; set; } = "";
        public bool IsCustomized { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; } = true;
        public List<MessageRow> Items { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            var overrides = (await db.QueryAsync<(string TemplateKey, string? TextAr, string? TextEn)>(
                "SELECT TemplateKey, TextAr, TextEn FROM PlatformMessage"))
                .ToDictionary(x => x.TemplateKey, x => x);

            var items = DefaultMessages.Defaults.Select(pair =>
            {
                var customized = overrides.ContainsKey(pair.Key);
                var row = customized ? overrides[pair.Key] : default;
                return new MessageRow
                {
                    Key = pair.Key,
                    DefaultAr = pair.Value.Ar,
                    DefaultEn = pair.Value.En,
                    Ar = customized && !string.IsNullOrWhiteSpace(row.TextAr) ? row.TextAr! : pair.Value.Ar,
                    En = customized && !string.IsNullOrWhiteSpace(row.TextEn) ? row.TextEn! : pair.Value.En,
                    IsCustomized = customized
                };
            }).ToList();

            return new Result { Items = items };
        }
    }
}

public class UpdatePlatformMessage
{
    public class Command : IRequest<Result>
    {
        public string? ActorUserId { get; set; }
        public string TemplateKey { get; set; } = "";
        public string? TextAr { get; set; }
        public string? TextEn { get; set; }
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
            RuleFor(x => x.TemplateKey).NotEmpty();
            RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.TextAr) || !string.IsNullOrWhiteSpace(x.TextEn))
                .WithMessage("At least one language is required.");
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (!DefaultMessages.Defaults.ContainsKey(request.TemplateKey))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            foreach (var text in new[] { request.TextAr, request.TextEn })
            {
                var unknown = MessageTemplateVars.FindUnknown(text ?? "");
                if (unknown is not null)
                    return new Result { Success = false, ErrorCode = ErrorCodes.UnknownVariable };
            }

            using var db = _mdb.Open();
            await db.ExecuteAsync(
                """
                INSERT INTO PlatformMessage (TemplateKey, TextAr, TextEn, UpdatedAt)
                VALUES (@TemplateKey, @TextAr, @TextEn, UTC_TIMESTAMP())
                ON DUPLICATE KEY UPDATE
                    TextAr = VALUES(TextAr),
                    TextEn = VALUES(TextEn),
                    UpdatedAt = UTC_TIMESTAMP()
                """,
                request);

            await ActivityLogger.LogAsync(
                db, null, null, "platform_message", request.TemplateKey, "content.platform_message_updated",
                request.ActorUserId);

            return new Result { Success = true };
        }
    }
}

public class ResetPlatformMessage
{
    public class Command : IRequest<Result>
    {
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
        public CommandValidator() => RuleFor(x => x.TemplateKey).NotEmpty();
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (!DefaultMessages.Defaults.ContainsKey(request.TemplateKey))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            using var db = _mdb.Open();
            await db.ExecuteAsync(
                "DELETE FROM PlatformMessage WHERE TemplateKey = @TemplateKey",
                new { request.TemplateKey });

            await ActivityLogger.LogAsync(
                db, null, null, "platform_message", request.TemplateKey, "content.platform_message_reset",
                request.ActorUserId);

            return new Result { Success = true };
        }
    }
}
