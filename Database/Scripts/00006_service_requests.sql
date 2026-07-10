-- DMN-503 service request schema: requests, internal notes, status history,
-- and customer-uploaded attachments (BP §10.16/10.18/10.19/§18).
--
-- Rules baked into this schema:
-- - No DELETE paths: requests, notes, history, and attachments are append/update
--   only. Closing a request sets Status/CloseOutcome/ClosedAt (BP §18).
-- - Customer identity (CustomerName/CustomerPhone) is duplicated onto the
--   request INTENTIONALLY: the public submitter may differ from the warranty's
--   customer, and the warranty customer row may change later.
-- - RequestNumber is the customer-facing identifier: global SR-{yyMM}-{seq},
--   unique (same scheme as Warranty.Code).
-- - ServiceRequestNote is internal-only; never included in public responses.
-- - ServiceRequestStatusHistory.ChangedByUserId NULL = customer/system action.
-- - Attachment.FilePath is a random relative path under a NON-public uploads
--   root; files are served only via an authenticated endpoint (DMN-602),
--   never from a public static directory.

CREATE TABLE IF NOT EXISTS ServiceRequest (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    WarrantyId VARCHAR(36) NOT NULL,
    RequestNumber VARCHAR(16) NOT NULL,
    CustomerName VARCHAR(120) NOT NULL,
    CustomerPhone VARCHAR(32) NOT NULL,
    ProblemType VARCHAR(40) NOT NULL,
    Description TEXT NULL,
    PreferredContact VARCHAR(16) NULL,
    Status VARCHAR(24) NOT NULL DEFAULT 'new',
    Source VARCHAR(16) NOT NULL DEFAULT 'public',
    AssignedToUserId VARCHAR(36) NULL,
    CloseOutcome VARCHAR(24) NULL,
    ClosedAt DATETIME NULL,
    ConsentAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_ServiceRequest_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_ServiceRequest_Warranty FOREIGN KEY (WarrantyId) REFERENCES Warranty(Id),
    CONSTRAINT FK_ServiceRequest_AssignedTo FOREIGN KEY (AssignedToUserId) REFERENCES User(Id),
    CONSTRAINT UX_ServiceRequest_RequestNumber UNIQUE (RequestNumber)
);

CREATE INDEX IX_ServiceRequest_Shop_Status_CreatedAt ON ServiceRequest (ShopId, Status, CreatedAt);
CREATE INDEX IX_ServiceRequest_WarrantyId ON ServiceRequest (WarrantyId);
CREATE INDEX IX_ServiceRequest_Shop_CustomerPhone ON ServiceRequest (ShopId, CustomerPhone);

CREATE TABLE IF NOT EXISTS ServiceRequestNote (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ServiceRequestId VARCHAR(36) NOT NULL,
    AuthorUserId VARCHAR(36) NOT NULL,
    Note TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_ServiceRequestNote_Request FOREIGN KEY (ServiceRequestId) REFERENCES ServiceRequest(Id),
    CONSTRAINT FK_ServiceRequestNote_Author FOREIGN KEY (AuthorUserId) REFERENCES User(Id)
);

CREATE INDEX IX_ServiceRequestNote_Request_CreatedAt ON ServiceRequestNote (ServiceRequestId, CreatedAt);

CREATE TABLE IF NOT EXISTS ServiceRequestStatusHistory (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ServiceRequestId VARCHAR(36) NOT NULL,
    FromStatus VARCHAR(24) NULL,
    ToStatus VARCHAR(24) NOT NULL,
    Note VARCHAR(500) NULL,
    NotifiedCustomer TINYINT(1) NOT NULL DEFAULT 0,
    ChangedByUserId VARCHAR(36) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_ServiceRequestStatusHistory_Request FOREIGN KEY (ServiceRequestId) REFERENCES ServiceRequest(Id),
    CONSTRAINT FK_ServiceRequestStatusHistory_ChangedBy FOREIGN KEY (ChangedByUserId) REFERENCES User(Id)
);

CREATE INDEX IX_ServiceRequestStatusHistory_Request_CreatedAt ON ServiceRequestStatusHistory (ServiceRequestId, CreatedAt);

CREATE TABLE IF NOT EXISTS Attachment (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    ServiceRequestId VARCHAR(36) NOT NULL,
    FilePath VARCHAR(255) NOT NULL,
    OriginalName VARCHAR(200) NOT NULL,
    ContentType VARCHAR(100) NOT NULL,
    SizeBytes INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Attachment_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_Attachment_Request FOREIGN KEY (ServiceRequestId) REFERENCES ServiceRequest(Id)
);

CREATE INDEX IX_Attachment_ServiceRequestId ON Attachment (ServiceRequestId);
