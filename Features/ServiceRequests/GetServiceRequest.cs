using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Full service-request detail for the shop case screen (DMN-602).
public class GetServiceRequest
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? RequestId { get; set; }
    }

    public class WarrantySummary
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public string? Status { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class AttachmentMeta
    {
        public string? Id { get; set; }
        public string? OriginalName { get; set; }
        public string? ContentType { get; set; }
        public int SizeBytes { get; set; }
    }

    public class NoteItem
    {
        public string? Id { get; set; }
        public string? AuthorName { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StatusHistoryItem
    {
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string? Note { get; set; }
        public bool NotifiedCustomer { get; set; }
        public string? ActorName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RequestDetail
    {
        public string? Id { get; set; }
        public string? RequestNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? ProblemType { get; set; }
        public string? Description { get; set; }
        public string? PreferredContact { get; set; }
        public string? Status { get; set; }
        public string? Source { get; set; }
        public string? CloseOutcome { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? WarrantyId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public RequestDetail? Request { get; set; }
        public WarrantySummary? Warranty { get; set; }
        public List<AttachmentMeta> Attachments { get; set; } = new();
        public List<NoteItem> Notes { get; set; } = new();
        public List<StatusHistoryItem> StatusHistory { get; set; } = new();
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            using var db = _mdb.Open();
            var detail = await db.QueryFirstOrDefaultAsync<RequestDetail>(
                """
                SELECT sr.Id, sr.RequestNumber, sr.CustomerName, sr.CustomerPhone,
                       sr.ProblemType, sr.Description, sr.PreferredContact, sr.Status, sr.Source,
                       sr.CloseOutcome, sr.ClosedAt, sr.AssignedToUserId, sr.CreatedAt, sr.WarrantyId,
                       u.FullName AS AssignedToName
                FROM ServiceRequest sr
                LEFT JOIN User u ON u.Id = sr.AssignedToUserId
                WHERE sr.Id = @RequestId AND sr.ShopId = @ShopId
                """,
                new { request.RequestId, request.ShopId });

            if (detail == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            var warranty = await db.QueryFirstOrDefaultAsync<WarrantySummary>(
                """
                SELECT w.Id, w.Code, w.ProductName,
                       CASE WHEN w.Status = @Active AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()
                            THEN 'expired' ELSE w.Status END AS Status,
                       w.ExpiryDate
                FROM Warranty w
                WHERE w.Id = @WarrantyId AND w.ShopId = @ShopId
                """,
                new { WarrantyId = detail.WarrantyId, request.ShopId, Active = WarrantyStatuses.Active });

            var attachments = (await db.QueryAsync<AttachmentMeta>(
                """
                SELECT Id, OriginalName, ContentType, SizeBytes
                FROM Attachment
                WHERE ServiceRequestId = @RequestId AND ShopId = @ShopId
                ORDER BY CreatedAt
                """,
                new { request.RequestId, request.ShopId })).ToList();

            var notes = (await db.QueryAsync<NoteItem>(
                """
                SELECT n.Id, u.FullName AS AuthorName, n.Note, n.CreatedAt
                FROM ServiceRequestNote n
                JOIN User u ON u.Id = n.AuthorUserId
                WHERE n.ServiceRequestId = @RequestId
                ORDER BY n.CreatedAt DESC
                """,
                new { request.RequestId })).ToList();

            var statusHistory = (await db.QueryAsync<StatusHistoryItem>(
                """
                SELECT h.FromStatus, h.ToStatus, h.Note, h.NotifiedCustomer,
                       u.FullName AS ActorName, h.CreatedAt
                FROM ServiceRequestStatusHistory h
                LEFT JOIN User u ON u.Id = h.ChangedByUserId
                WHERE h.ServiceRequestId = @RequestId
                ORDER BY h.CreatedAt DESC
                """,
                new { request.RequestId })).ToList();

            return new Result
            {
                Success = true,
                Request = detail,
                Warranty = warranty,
                Attachments = attachments,
                Notes = notes,
                StatusHistory = statusHistory
            };
        }
    }
}
