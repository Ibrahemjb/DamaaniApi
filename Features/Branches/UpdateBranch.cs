using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Branches;

public class UpdateBranch
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string BranchId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? City { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
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
            RuleFor(x => x.BranchId).NotEmpty();
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
            var updated = await db.ExecuteAsync(
                """
                UPDATE Branch
                SET Name = @Name, City = @City, Phone = @Phone, Address = @Address, UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @BranchId AND ShopId = @ShopId
                """,
                new
                {
                    request.BranchId,
                    request.ShopId,
                    Name = request.Name.Trim(),
                    City = NullIfBlank(request.City),
                    Phone = NullIfBlank(request.Phone),
                    Address = NullIfBlank(request.Address)
                });

            if (updated == 0)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            await ActivityLogger.LogAsync(
                db, null, request.ShopId, "branch", request.BranchId, "branch.updated", request.ActorUserId);

            return new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
