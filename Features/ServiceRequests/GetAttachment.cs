using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.ServiceRequests;

// Verify shop ownership and return file metadata for controller streaming (DMN-602).
public class GetAttachment
{
    public class Query : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public string? AttachmentId { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? FilePath { get; set; }
        public string? ContentType { get; set; }
        public string? OriginalName { get; set; }
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
            var row = await db.QueryFirstOrDefaultAsync<AttachmentRow>(
                """
                SELECT a.FilePath, a.ContentType, a.OriginalName
                FROM Attachment a
                JOIN ServiceRequest sr ON sr.Id = a.ServiceRequestId
                WHERE a.Id = @AttachmentId AND a.ShopId = @ShopId AND sr.ShopId = @ShopId
                """,
                new { request.AttachmentId, request.ShopId });

            if (row == null)
                return new Result { Success = false, ErrorCode = ErrorCodes.NotFound };

            return new Result
            {
                Success = true,
                FilePath = row.FilePath,
                ContentType = row.ContentType,
                OriginalName = row.OriginalName
            };
        }

        private sealed class AttachmentRow
        {
            public string? FilePath { get; set; }
            public string? ContentType { get; set; }
            public string? OriginalName { get; set; }
        }
    }
}
