using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Warranties;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Customers;

// Edit customer profile fields. Phone changes stay unique per shop; linked
// warranties keep CustomerId. Service requests continue to match by phone.
public class UpdateCustomer
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? CustomerId { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
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
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+?[0-9\s\-()]{7,20}$");
            RuleFor(x => x.City).MaximumLength(80);
            RuleFor(x => x.Address).MaximumLength(255);
            RuleFor(x => x.Notes).MaximumLength(500);
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

            var phone = CreateWarranty.CommandHandler.NormalizePhone(request.Phone);
            var name = request.Name.Trim();
            var city = NullIfBlank(request.City);
            var address = NullIfBlank(request.Address);
            var notes = NullIfBlank(request.Notes);

            using var db = _mdb.Open();
            var existing = await db.QueryFirstOrDefaultAsync<ExistingCustomer>(
                "SELECT Id, Phone FROM Customer WHERE Id = @CustomerId AND ShopId = @ShopId",
                new { request.CustomerId, request.ShopId });

            if (existing == null || string.IsNullOrEmpty(existing.Id))
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            if (phone != existing.Phone)
            {
                var conflict = await db.ExecuteScalarAsync<string?>(
                    "SELECT Id FROM Customer WHERE ShopId = @ShopId AND Phone = @Phone AND Id <> @CustomerId LIMIT 1",
                    new { request.ShopId, Phone = phone, request.CustomerId });
                if (!string.IsNullOrEmpty(conflict))
                    return new Result { Success = false, ErrorCode = ErrorCodes.PhoneTaken };
            }

            await db.ExecuteAsync(
                """
                UPDATE Customer
                SET Name = @Name,
                    Phone = @Phone,
                    City = @City,
                    Address = @Address,
                    Notes = @Notes,
                    UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @CustomerId AND ShopId = @ShopId
                """,
                new
                {
                    request.CustomerId,
                    request.ShopId,
                    Name = name,
                    Phone = phone,
                    City = city,
                    Address = address,
                    Notes = notes
                });

            return new Result { Success = true };
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private class ExistingCustomer
    {
        public string? Id { get; set; }
        public string? Phone { get; set; }
    }
}
