using Dapper;
using System.Data;

namespace DammaniAPI.Features.ServiceRequests;

internal static class ServiceRequestAccess
{
    internal sealed class RequestRow
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public string? ShopId { get; set; }
    }

    internal static bool IsClosed(string? status)
        => string.Equals(status, ServiceRequestStatuses.Closed, StringComparison.OrdinalIgnoreCase);

    internal static async Task<RequestRow?> LoadAsync(
        IDbConnection db, IDbTransaction? tx, string shopId, string requestId)
        => await db.QueryFirstOrDefaultAsync<RequestRow>(
            "SELECT Id, Status, ShopId FROM ServiceRequest WHERE Id = @RequestId AND ShopId = @ShopId",
            new { RequestId = requestId, ShopId = shopId }, tx);
}
