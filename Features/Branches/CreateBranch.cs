using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Branches;

public class CreateBranch
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string Name { get; set; } = "";
        public string? City { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? BranchId { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.City).MaximumLength(80).When(x => !string.IsNullOrWhiteSpace(x.City));
            RuleFor(x => x.Phone).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.Phone));
            RuleFor(x => x.Address).MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Address));
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
            var hasBranches = await db.ExecuteScalarAsync<bool?>(
                """
                SELECT p.HasBranches FROM Subscription sub
                JOIN Plan p ON p.Id = sub.PlanId WHERE sub.ShopId = @ShopId
                """,
                new { request.ShopId });
            if (hasBranches != true)
                return new Result { Success = false, ErrorCode = ErrorCodes.FeatureNotInPlan };

            var branchId = Guid.NewGuid().ToString();
            await db.ExecuteAsync(
                """
                INSERT INTO Branch (Id, ShopId, Name, City, Phone, Address, Status, CreatedAt)
                VALUES (@Id, @ShopId, @Name, @City, @Phone, @Address, @Active, UTC_TIMESTAMP())
                """,
                new
                {
                    Id = branchId,
                    request.ShopId,
                    Name = request.Name.Trim(),
                    City = NullIfBlank(request.City),
                    Phone = NullIfBlank(request.Phone),
                    Address = NullIfBlank(request.Address),
                    Active = BranchStatuses.Active
                });

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "branch", branchId, "branch.created", request.ActorUserId);

            return new Result { Success = true, BranchId = branchId };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
