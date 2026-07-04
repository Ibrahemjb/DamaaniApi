-- DMN-201 identity and shop tenancy foundation.
-- MVP assumption: one shop per owner during signup; ShopUser keeps multi-shop support possible later.

ALTER TABLE User
    ADD COLUMN FullName VARCHAR(120) NOT NULL DEFAULT '',
    ADD COLUMN Phone VARCHAR(32) NULL,
    ADD COLUMN PasswordHash VARCHAR(255) NULL,
    ADD COLUMN Language VARCHAR(2) NOT NULL DEFAULT 'ar',
    ADD COLUMN Status VARCHAR(16) NOT NULL DEFAULT 'active',
    ADD COLUMN IsPlatformAdmin TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN UpdatedAt DATETIME NULL;

CREATE UNIQUE INDEX UX_User_Email ON User (Email);
CREATE INDEX IX_User_Phone ON User (Phone);

CREATE TABLE IF NOT EXISTS Shop (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    Name VARCHAR(160) NOT NULL,
    LogoPath VARCHAR(255) NULL,
    Phone VARCHAR(32) NULL,
    WhatsAppNumber VARCHAR(32) NULL,
    Address VARCHAR(255) NULL,
    City VARCHAR(80) NULL,
    Country VARCHAR(2) NOT NULL DEFAULT 'PS',
    BusinessCategory VARCHAR(40) NULL,
    Status VARCHAR(16) NOT NULL DEFAULT 'active',
    SuspensionNote VARCHAR(500) NULL,
    OnboardingCompletedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);

CREATE TABLE IF NOT EXISTS ShopUser (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    UserId VARCHAR(36) NOT NULL,
    Role VARCHAR(16) NOT NULL,
    Status VARCHAR(16) NOT NULL DEFAULT 'active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_ShopUser_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_ShopUser_User FOREIGN KEY (UserId) REFERENCES User(Id),
    CONSTRAINT UX_ShopUser_Shop_User UNIQUE (ShopId, UserId)
);

CREATE INDEX IX_ShopUser_UserId ON ShopUser (UserId);

CREATE TABLE IF NOT EXISTS PasswordResetToken (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    UserId VARCHAR(36) NOT NULL,
    TokenHash VARCHAR(255) NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_PasswordResetToken_User FOREIGN KEY (UserId) REFERENCES User(Id)
);

CREATE INDEX IX_PasswordResetToken_UserId ON PasswordResetToken (UserId);
