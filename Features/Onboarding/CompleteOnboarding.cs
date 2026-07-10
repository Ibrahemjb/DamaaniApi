using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Utilities;
using MediatR;

namespace DammaniAPI.Features.Onboarding;

public class CompleteOnboarding
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
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
                var updated = await db.ExecuteAsync(
                    """
                    UPDATE Shop
                    SET OnboardingCompletedAt = UTC_TIMESTAMP(), UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @ShopId AND OnboardingCompletedAt IS NULL
                    """,
                    new { request.ShopId },
                    tx);

                if (updated == 0)
                {
                    var exists = await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM Shop WHERE Id = @ShopId",
                        new { request.ShopId },
                        tx);
                    if (exists == 0)
                    {
                        tx.Rollback();
                        return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
                    }
                }
                else
                {
                    await ActivityLogger.LogAsync(
                        db, tx, request.ShopId, "shop", request.ShopId, "shop.onboarded", request.ActorUserId);
                }

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
