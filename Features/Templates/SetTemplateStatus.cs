using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Templates;

public class SetTemplateStatus
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string TemplateId { get; set; } = "";
        public bool Active { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.TemplateId).NotEmpty();
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
            using var tx = db.BeginTransaction();
            try
            {
                // Deactivation only hides the template from new warranties;
                // existing warranties keep their snapshotted terms (BP §18).
                var affected = await db.ExecuteAsync(
                    """
                    UPDATE WarrantyTemplate
                    SET Status = @Status, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @TemplateId AND ShopId = @ShopId
                    """,
                    new
                    {
                        Status = request.Active ? TemplateStatuses.Active : TemplateStatuses.Inactive,
                        request.TemplateId,
                        request.ShopId
                    },
                    tx);

                if (affected == 0)
                {
                    tx.Rollback();
                    return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                }

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "template", request.TemplateId,
                    request.Active ? "template.reactivated" : "template.deactivated",
                    request.ActorUserId);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true };
        }
    }
}
