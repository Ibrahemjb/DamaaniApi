-- DMN-401 customer & warranty core schema with search indexes.
--
-- Rules baked into this schema (BP §10.9/10.11/§18):
-- - Warranty.Status holds draft/active/cancelled only. "Expired" is DERIVED at
--   read time (Status = 'active' AND ExpiryDate < CURDATE()) and never stored.
-- - Drafts may hold NULL PurchaseDate/DurationMonths/ExpiryDate.
-- - Warranties are NEVER hard-deleted: cancel sets Status/CancelReason/CancelledAt.
-- - TermsAr/TermsEn are a snapshot taken at creation; later template edits must
--   not change existing warranties (never re-read from WarrantyTemplate).
-- - SerialNumber duplicates WARN in the same shop but are allowed — no unique
--   index on it by design (BP §10.9).
-- - BranchId is a forward-compat column; the Branch table and its FK arrive
--   with DMN-901 (documented decision so this epic does not block on settings).
-- - PublicSlug is the only externally shared identifier: random, URL-safe,
--   >= 16 chars, globally unique (not guessable, not sequential).

CREATE TABLE IF NOT EXISTS Customer (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    Name VARCHAR(120) NOT NULL,
    Phone VARCHAR(32) NOT NULL,
    City VARCHAR(80) NULL,
    Address VARCHAR(255) NULL,
    Notes VARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Customer_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT UX_Customer_Shop_Phone UNIQUE (ShopId, Phone)
);

CREATE INDEX IX_Customer_Shop_Name ON Customer (ShopId, Name);

CREATE TABLE IF NOT EXISTS Warranty (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    CustomerId VARCHAR(36) NOT NULL,
    BranchId VARCHAR(36) NULL,
    TemplateId VARCHAR(36) NULL,
    Code VARCHAR(20) NOT NULL,
    PublicSlug VARCHAR(24) NOT NULL,
    ProductName VARCHAR(160) NOT NULL,
    Category VARCHAR(40) NULL,
    Model VARCHAR(120) NULL,
    SerialNumber VARCHAR(120) NULL,
    ColorSpecs VARCHAR(160) NULL,
    PurchaseReference VARCHAR(120) NULL,
    PurchaseDate DATE NULL,
    DurationMonths INT NULL,
    ExpiryDate DATE NULL,
    TermsAr TEXT NULL,
    TermsEn TEXT NULL,
    Status VARCHAR(16) NOT NULL DEFAULT 'active',
    CancelReason VARCHAR(300) NULL,
    CancelledAt DATETIME NULL,
    CreatedByUserId VARCHAR(36) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_Warranty_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_Warranty_Customer FOREIGN KEY (CustomerId) REFERENCES Customer(Id),
    CONSTRAINT FK_Warranty_Template FOREIGN KEY (TemplateId) REFERENCES WarrantyTemplate(Id),
    CONSTRAINT FK_Warranty_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES User(Id),
    CONSTRAINT UX_Warranty_Code UNIQUE (Code),
    CONSTRAINT UX_Warranty_PublicSlug UNIQUE (PublicSlug)
);

-- Search/filter indexes (BP §10.8/10.11: phone/serial/name/code/product lookups
-- must be instant; list filters by status, category, expiry window).
CREATE INDEX IX_Warranty_Shop_Status_CreatedAt ON Warranty (ShopId, Status, CreatedAt);
CREATE INDEX IX_Warranty_Shop_SerialNumber ON Warranty (ShopId, SerialNumber);
CREATE INDEX IX_Warranty_Shop_ExpiryDate ON Warranty (ShopId, ExpiryDate);
CREATE INDEX IX_Warranty_Shop_Category ON Warranty (ShopId, Category);
