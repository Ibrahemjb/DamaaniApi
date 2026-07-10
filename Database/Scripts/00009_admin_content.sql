-- DMN-1104: platform message overrides + help/FAQ content blocks.

CREATE TABLE IF NOT EXISTS PlatformMessage (
    TemplateKey VARCHAR(40) NOT NULL PRIMARY KEY,
    TextAr TEXT NULL,
    TextEn TEXT NULL,
    UpdatedAt DATETIME NULL
);

CREATE TABLE IF NOT EXISTS ContentBlock (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    BlockKey VARCHAR(60) NOT NULL,
    TitleAr VARCHAR(200) NULL,
    TitleEn VARCHAR(200) NULL,
    BodyAr TEXT NULL,
    BodyEn TEXT NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    IsPublished TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT UX_ContentBlock_Key UNIQUE (BlockKey)
);

INSERT INTO ContentBlock (Id, BlockKey, TitleAr, TitleEn, BodyAr, BodyEn, SortOrder)
VALUES
('cb000001-0001-4d10-9c5a-000000000001', 'faq_landing', 'ما هو ضماني؟', 'What is Damaani?', 'ضماني يحوّل كرت الضمان الورقي إلى كرت رقمي يتحقق منه الزبون عبر QR أو واتساب.', 'Damaani replaces paper warranty cards with digital cards customers verify via QR or WhatsApp.', 1),
('cb000001-0001-4d10-9c5a-000000000002', 'faq_pricing', 'الأسعار', 'Pricing', 'خطط شهرية حسب عدد البطاقات الجديدة. الصفحات العامة وطلبات الصيانة لا تتوقف عند الحد.', 'Monthly plans by new cards created. Public pages and service requests are never blocked at the limit.', 2),
('cb000001-0001-4d10-9c5a-000000000003', 'help_getting_started', 'البداية', 'Getting started', 'أنشئ محلك، اختر القوالب، وأصدر أول ضمان في أقل من دقيقة.', 'Create your shop, pick templates, and issue your first warranty in under a minute.', 3),
('cb000001-0001-4d10-9c5a-000000000004', 'help_warranties', 'الضمانات', 'Warranties', 'كل ضمان يحصل على رمز عام ورابط QR للزبون.', 'Each warranty gets a public code and QR link for the customer.', 4),
('cb000001-0001-4d10-9c5a-000000000005', 'help_requests', 'طلبات الصيانة', 'Service requests', 'الزبون يرسل الطلب من صفحة الضمان العامة ويتابع الحالة هناك.', 'Customers submit from the public warranty page and track status there.', 5),
('cb000001-0001-4d10-9c5a-000000000006', 'help_billing', 'الفوترة', 'Billing', 'الترقية تبدأ بطلب من صاحب المحل وتُفعّل بعد تأكيد الدفع اليدوي.', 'Upgrades start as a shop-owner request and activate after manual payment confirmation.', 6),
('cb000001-0001-4d10-9c5a-000000000007', 'help_troubleshooting', 'مشاكل شائعة', 'Troubleshooting', 'إذا لم يظهر QR، تأكد أن الضمان نشط وأن المحل غير موقوف.', 'If QR does not load, confirm the warranty is active and the shop is not suspended.', 7)
ON DUPLICATE KEY UPDATE BlockKey = BlockKey;
