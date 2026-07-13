-- Email verification on signup. Accounts created before this migration are
-- grandfathered in as verified so they are not locked out of login.

ALTER TABLE User
    ADD COLUMN EmailVerifiedAt DATETIME NULL;

UPDATE User SET EmailVerifiedAt = CreatedAt WHERE EmailVerifiedAt IS NULL;

CREATE TABLE IF NOT EXISTS EmailVerificationCode (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    UserId VARCHAR(36) NOT NULL,
    CodeHash VARCHAR(255) NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_EmailVerificationCode_User FOREIGN KEY (UserId) REFERENCES User(Id)
);

CREATE INDEX IX_EmailVerificationCode_UserId ON EmailVerificationCode (UserId);
