-- DMN-901: shop settings columns, MessageTemplate, StaffInvite, Branch,
-- and the deferred Warranty.BranchId FK (DMN-401).

ALTER TABLE Shop
    ADD COLUMN PublicLanguage VARCHAR(2) NOT NULL DEFAULT 'ar',
    ADD COLUMN PublicShowAddress TINYINT(1) NOT NULL DEFAULT 1,
    ADD COLUMN PublicShowWhatsApp TINYINT(1) NOT NULL DEFAULT 1,
    ADD COLUMN PublicTheme VARCHAR(20) NOT NULL DEFAULT 'default',
    ADD COLUMN AllowExpiredRequests TINYINT(1) NOT NULL DEFAULT 1,
    ADD COLUMN DefaultWarrantyDurationMonths INT NULL,
    ADD COLUMN EmailAlertsEnabled TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN AlertEmail VARCHAR(255) NULL;

CREATE TABLE IF NOT EXISTS MessageTemplate (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    TemplateKey VARCHAR(40) NOT NULL,
    TextAr TEXT NULL,
    TextEn TEXT NULL,
    UpdatedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_MessageTemplate_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT UX_MessageTemplate_Shop_Key UNIQUE (ShopId, TemplateKey)
);

CREATE TABLE IF NOT EXISTS StaffInvite (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    Email VARCHAR(255) NULL,
    Phone VARCHAR(32) NULL,
    Role VARCHAR(16) NOT NULL DEFAULT 'staff',
    TokenHash VARCHAR(255) NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    AcceptedAt DATETIME NULL,
    CreatedByUserId VARCHAR(36) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_StaffInvite_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_StaffInvite_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES User(Id)
);

CREATE INDEX IX_StaffInvite_Shop ON StaffInvite (ShopId);

CREATE TABLE IF NOT EXISTS Branch (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    Name VARCHAR(120) NOT NULL,
    City VARCHAR(80) NULL,
    Phone VARCHAR(32) NULL,
    Address VARCHAR(255) NULL,
    Status VARCHAR(16) NOT NULL DEFAULT 'active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Branch_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id)
);

CREATE INDEX IX_Branch_Shop_Status ON Branch (ShopId, Status);

ALTER TABLE Warranty
    ADD CONSTRAINT FK_Warranty_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);
