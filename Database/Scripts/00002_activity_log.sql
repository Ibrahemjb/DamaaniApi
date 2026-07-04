-- DMN-107 append-only audit/activity log.

CREATE TABLE IF NOT EXISTS ActivityLog (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NULL,
    EntityType VARCHAR(40) NOT NULL,
    EntityId VARCHAR(36) NOT NULL,
    Action VARCHAR(60) NOT NULL,
    ActorUserId VARCHAR(36) NULL,
    Details JSON NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_ActivityLog_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_ActivityLog_User FOREIGN KEY (ActorUserId) REFERENCES User(Id)
);

CREATE INDEX IX_ActivityLog_Entity ON ActivityLog (EntityType, EntityId, CreatedAt);
CREATE INDEX IX_ActivityLog_Shop ON ActivityLog (ShopId, CreatedAt);
