using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Templates;

public class DuplicateTemplate
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string TemplateId { get; set; } = "";
        public string? NewName { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.TemplateId).NotEmpty();
            RuleFor(x => x.NewName).MaximumLength(120).When(x => !string.IsNullOrWhiteSpace(x.NewName));
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
            var sourceName = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT Name FROM WarrantyTemplate WHERE Id = @TemplateId AND ShopId = @ShopId",
                new { request.TemplateId, request.ShopId });

            if (sourceName == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var id = Guid.NewGuid().ToString();
            var name = string.IsNullOrWhiteSpace(request.NewName)
                ? CopyName(sourceName)
                : request.NewName.Trim();

            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    """
                    INSERT INTO WarrantyTemplate
                        (Id, ShopId, Name, Category, DurationMonths, TermsAr, TermsEn,
                         ExclusionsAr, ExclusionsEn, ServiceInstructionsAr, ServiceInstructionsEn, Status)
                    SELECT @Id, ShopId, @Name, Category, DurationMonths, TermsAr, TermsEn,
                           ExclusionsAr, ExclusionsEn, ServiceInstructionsAr, ServiceInstructionsEn, @Status
                    FROM WarrantyTemplate
                    WHERE Id = @TemplateId AND ShopId = @ShopId
                    """,
                    new { Id = id, Name = name, Status = TemplateStatuses.Active, request.TemplateId, request.ShopId },
                    tx);
                await ActivityLogger.LogAsync(db, tx, request.ShopId, "template", id, "template.duplicated", request.ActorUserId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, Id = id, Name = name };
        }

        private static string CopyName(string sourceName)
        {
            // U+0600–U+06FF: Arabic block — Arabic-named templates get the Arabic suffix.
            var suffix = sourceName.Any(c => c >= '؀' && c <= 'ۿ') ? " (نسخة)" : " (copy)";
            var maxBaseLength = 120 - suffix.Length;
            var baseName = sourceName.Length > maxBaseLength ? sourceName[..maxBaseLength] : sourceName;
            return baseName + suffix;
        }
    }
}
