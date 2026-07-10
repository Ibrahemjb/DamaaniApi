using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features.Warranties;
using DammaniAPI.Utilities;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Shop-initiated service request from warranty detail (DMN-603, BP §10.12).
// Same validation as public submit minus consent/files; Source='internal'.
public class CreateInternal
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? ActorUserId { get; set; }
        public string? WarrantyId { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string ProblemType { get; set; } = "";
        public string Description { get; set; } = "";
        public string? PreferredContact { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.WarrantyId).NotEmpty();
            RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.CustomerPhone)
                .NotEmpty()
                .Matches(@"^\+?[0-9\s\-()]{7,20}$");
            RuleFor(x => x.ProblemType)
                .NotEmpty()
                .Must(x => ProblemTypes.Supported.Contains(x));
            RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
            RuleFor(x => x.PreferredContact)
                .Must(x => PreferredContacts.Supported.Contains(x!))
                .When(x => !string.IsNullOrWhiteSpace(x.PreferredContact));
        }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ActorUserId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            var warranty = await db.QueryFirstOrDefaultAsync<WarrantyRow>(
                """
                SELECT Id, Status
                FROM Warranty
                WHERE Id = @WarrantyId AND ShopId = @ShopId
                """,
                new { request.WarrantyId, request.ShopId });

            if (warranty == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };
            if (warranty.Status == WarrantyStatuses.Draft || warranty.Status == WarrantyStatuses.Cancelled)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotAllowed };

            var requestId = Guid.NewGuid().ToString();
            string requestNumber;
            using var tx = db.BeginTransaction();
            try
            {
                requestNumber = await ServiceRequestNumberHelper.InsertAsync(db, tx, new
                {
                    Id = requestId,
                    request.ShopId,
                    WarrantyId = warranty.Id,
                    CustomerName = request.CustomerName.Trim(),
                    CustomerPhone = CreateWarranty.CommandHandler.NormalizePhone(request.CustomerPhone),
                    request.ProblemType,
                    Description = request.Description.Trim(),
                    PreferredContact = CreateWarranty.CommandHandler.NullIfBlank(request.PreferredContact),
                    Status = ServiceRequestStatuses.New,
                    Source = ServiceRequestSources.Internal
                }, setConsentAt: false);

                await db.ExecuteAsync(
                    """
                    INSERT INTO ServiceRequestStatusHistory (Id, ServiceRequestId, FromStatus, ToStatus, ChangedByUserId)
                    VALUES (@Id, @ServiceRequestId, NULL, @ToStatus, @ActorUserId)
                    """,
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        ServiceRequestId = requestId,
                        ToStatus = ServiceRequestStatuses.New,
                        request.ActorUserId
                    }, tx);

                await ActivityLogger.LogAsync(
                    db, tx, request.ShopId, "request", requestId, "request.created_internal",
                    request.ActorUserId,
                    $$"""{"requestNumber":"{{requestNumber}}"}""");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return new Result { Success = true, Id = requestId, RequestNumber = requestNumber };
        }

        private sealed class WarrantyRow
        {
            public string? Id { get; set; }
            public string? Status { get; set; }
        }
    }
}
