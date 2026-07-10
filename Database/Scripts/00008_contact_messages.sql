-- DMN-1204: async contact form storage for help center submissions.
CREATE TABLE IF NOT EXISTS ContactMessage (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    Name VARCHAR(120) NULL,
    Email VARCHAR(255) NOT NULL,
    Topic VARCHAR(40) NULL,
    Message TEXT NOT NULL,
    ShopId VARCHAR(36) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_ContactMessage_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id)
);

CREATE INDEX IX_ContactMessage_CreatedAt ON ContactMessage (CreatedAt);
