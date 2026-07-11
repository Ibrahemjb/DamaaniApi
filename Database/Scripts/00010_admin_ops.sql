-- Platform ops expansion: contact inbox status, admin RBAC role.
ALTER TABLE ContactMessage
    ADD COLUMN Status VARCHAR(16) NOT NULL DEFAULT 'unread',
    ADD COLUMN InternalNote VARCHAR(500) NULL,
    ADD COLUMN ResolvedAt DATETIME NULL,
    ADD COLUMN ResolvedByUserId VARCHAR(36) NULL;

CREATE INDEX IX_ContactMessage_Status_CreatedAt ON ContactMessage (Status, CreatedAt);

ALTER TABLE User
    ADD COLUMN AdminRole VARCHAR(16) NULL;

-- Existing platform admins default to super (full ops access).
UPDATE User SET AdminRole = 'super' WHERE IsPlatformAdmin = 1 AND AdminRole IS NULL;
