-- DMN-1001 monetization data model: Plan catalog + Subscription + Payment,
-- seeded with the four BP §5 plans, plus a Free-subscription backfill so every
-- shop always has an active subscription.
--
-- Semantics (BP §5/§13/§18):
-- - MonthlyCardLimit counts NEW warranty cards per month only; existing public
--   pages and service requests are never blocked (enforced in DMN-1002).
-- - Downgrades apply at next cycle: ScheduledPlanId holds the pending plan and
--   is applied at CurrentPeriodEnd (lazy rollover, DMN-1004). Manual-mode
--   upgrade requests also park the target plan here until admin confirmation.
-- - CancelAtPeriodEnd keeps data accessible; cancelling drops the shop to Free
--   limits at period end (no hard delete).
-- - HasAnalytics marks the Business-tier marketing point only — the reports
--   page itself is available to all plans.
-- - Starter's logo / WhatsApp share / service-request inclusions are not
--   modeled as flags in MVP; only the gates consumed by DMN-402/409/904/905
--   are columns here.
-- - Prices are stored in both currencies verbatim from BP §5 (no FX math).

CREATE TABLE IF NOT EXISTS Plan (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    Code VARCHAR(20) NOT NULL,
    NameEn VARCHAR(60) NOT NULL,
    NameAr VARCHAR(60) NOT NULL,
    PriceUsd DECIMAL(8,2) NOT NULL,
    PriceIls DECIMAL(8,2) NOT NULL,
    MonthlyCardLimit INT NOT NULL,
    MaxUsers INT NOT NULL,
    HasBranches TINYINT(1) NOT NULL DEFAULT 0,
    HasExport TINYINT(1) NOT NULL DEFAULT 0,
    HasCustomTemplates TINYINT(1) NOT NULL DEFAULT 0,
    HasPrintableLabels TINYINT(1) NOT NULL DEFAULT 0,
    ShowDamaaniBranding TINYINT(1) NOT NULL DEFAULT 0,
    HasAnalytics TINYINT(1) NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT UX_Plan_Code UNIQUE (Code)
);

INSERT INTO Plan
    (Id, Code, NameEn, NameAr, PriceUsd, PriceIls, MonthlyCardLimit, MaxUsers, HasBranches, HasExport, HasCustomTemplates, HasPrintableLabels, ShowDamaaniBranding, HasAnalytics, SortOrder, IsActive)
VALUES
('5b2d0a10-0001-4d10-9c5a-000000000001', 'free', 'Free', 'المجانية', 0.00, 0.00, 30, 1, 0, 0, 0, 0, 1, 0, 1, 1),
('5b2d0a10-0001-4d10-9c5a-000000000002', 'starter', 'Starter', 'Starter', 9.00, 35.00, 300, 2, 0, 0, 0, 0, 0, 0, 2, 1),
('5b2d0a10-0001-4d10-9c5a-000000000003', 'pro', 'Pro', 'Pro', 19.00, 75.00, 1500, 5, 1, 1, 1, 1, 0, 0, 3, 1),
('5b2d0a10-0001-4d10-9c5a-000000000004', 'business', 'Business', 'Business', 39.00, 150.00, 5000, 15, 1, 1, 1, 1, 0, 1, 4, 1);

CREATE TABLE IF NOT EXISTS Subscription (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    PlanId VARCHAR(36) NOT NULL,
    Status VARCHAR(16) NOT NULL DEFAULT 'active',
    CurrentPeriodStart DATE NOT NULL,
    CurrentPeriodEnd DATE NOT NULL,
    ScheduledPlanId VARCHAR(36) NULL,
    CancelAtPeriodEnd TINYINT(1) NOT NULL DEFAULT 0,
    CancelReason VARCHAR(300) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT UX_Subscription_Shop UNIQUE (ShopId),
    CONSTRAINT FK_Subscription_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_Subscription_Plan FOREIGN KEY (PlanId) REFERENCES Plan(Id),
    CONSTRAINT FK_Subscription_ScheduledPlan FOREIGN KEY (ScheduledPlanId) REFERENCES Plan(Id)
);

CREATE TABLE IF NOT EXISTS Payment (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    SubscriptionId VARCHAR(36) NOT NULL,
    PlanId VARCHAR(36) NOT NULL,
    AmountUsd DECIMAL(8,2) NOT NULL,
    AmountIls DECIMAL(8,2) NOT NULL,
    Method VARCHAR(20) NOT NULL,
    Status VARCHAR(16) NOT NULL,
    Reference VARCHAR(100) NULL,
    PaidAt DATETIME NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Payment_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id),
    CONSTRAINT FK_Payment_Subscription FOREIGN KEY (SubscriptionId) REFERENCES Subscription(Id),
    CONSTRAINT FK_Payment_Plan FOREIGN KEY (PlanId) REFERENCES Plan(Id)
);

CREATE INDEX IX_Payment_Shop_PaidAt ON Payment (ShopId, PaidAt);

-- Backfill: every existing shop gets an active Free subscription for the
-- current calendar month (UTC). New shops get theirs at signup (DMN-203 touch).
INSERT INTO Subscription (Id, ShopId, PlanId, Status, CurrentPeriodStart, CurrentPeriodEnd, CreatedAt)
SELECT UUID(), s.Id, '5b2d0a10-0001-4d10-9c5a-000000000001', 'active', DATE_FORMAT(UTC_DATE(), '%Y-%m-01'), LAST_DAY(UTC_DATE()), UTC_TIMESTAMP()
FROM Shop s
WHERE NOT EXISTS (SELECT 1 FROM Subscription sub WHERE sub.ShopId = s.Id);
