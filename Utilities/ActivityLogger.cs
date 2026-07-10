using System.Data;
using Dapper;

namespace DammaniAPI.Utilities;

public static class ActivityLogger
{
    // Action convention: entity.verb_past, e.g. shop.created, user.password_reset, user.password_changed.
    // Admin mutations (DMN-1101+): always pass target ShopId when the action concerns a shop;
    // platform-only edits (content.*) may use ShopId=null; ActorUserId must be the admin user.
    public static async Task LogAsync(
        IDbConnection db,
        IDbTransaction? tx,
        string? shopId,
        string entityType,
        string entityId,
        string action,
        string? actorUserId,
        string? detailsJson = null)
    {
        await db.ExecuteAsync(
            """
            INSERT INTO ActivityLog (Id, ShopId, EntityType, EntityId, Action, ActorUserId, Details, CreatedAt)
            VALUES (@Id, @ShopId, @EntityType, @EntityId, @Action, @ActorUserId, @Details, UTC_TIMESTAMP())
            """,
            new
            {
                Id = Guid.NewGuid().ToString(),
                ShopId = shopId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                ActorUserId = actorUserId,
                Details = detailsJson
            },
            tx);
    }
}
