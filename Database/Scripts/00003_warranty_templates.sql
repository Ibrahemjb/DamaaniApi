-- DMN-301 shop warranty templates + platform default template seeds.
-- Templates are never hard-deleted (Status only); terms are snapshotted onto
-- warranties at creation, so template edits never affect old warranties.

CREATE TABLE IF NOT EXISTS WarrantyTemplate (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    ShopId VARCHAR(36) NOT NULL,
    Name VARCHAR(120) NOT NULL,
    Category VARCHAR(40) NOT NULL,
    DurationMonths INT NOT NULL,
    TermsAr TEXT NULL,
    TermsEn TEXT NULL,
    ExclusionsAr TEXT NULL,
    ExclusionsEn TEXT NULL,
    ServiceInstructionsAr TEXT NULL,
    ServiceInstructionsEn TEXT NULL,
    Status VARCHAR(16) NOT NULL DEFAULT 'active',
    LastUsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CONSTRAINT FK_WarrantyTemplate_Shop FOREIGN KEY (ShopId) REFERENCES Shop(Id)
);

CREATE INDEX IX_WarrantyTemplate_Shop_Status ON WarrantyTemplate (ShopId, Status);

-- Platform-managed defaults copied per shop during onboarding (DMN-206); never
-- referenced live by shops and edited only by platform admin (DMN-1104).
CREATE TABLE IF NOT EXISTS DefaultTemplate (
    Id VARCHAR(36) NOT NULL PRIMARY KEY,
    Name VARCHAR(120) NOT NULL,
    Category VARCHAR(40) NOT NULL,
    DurationMonths INT NOT NULL,
    TermsAr TEXT NULL,
    TermsEn TEXT NULL,
    ExclusionsAr TEXT NULL,
    ExclusionsEn TEXT NULL,
    ServiceInstructionsAr TEXT NULL,
    ServiceInstructionsEn TEXT NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);

INSERT INTO DefaultTemplate
    (Id, Name, Category, DurationMonths, TermsAr, TermsEn, ExclusionsAr, ExclusionsEn, ServiceInstructionsAr, ServiceInstructionsEn, SortOrder)
VALUES
(
    '9b1f4a60-0001-4d10-9c5a-000000000001',
    'Solar inverter standard',
    'solar_battery',
    24,
    'يشمل الضمان عيوب التصنيع في الانفرتر عند الاستخدام الطبيعي، ويغطي الإصلاح أو الاستبدال حسب تقييم الفني المعتمد.',
    'Warranty covers manufacturing defects in the inverter under normal use, including repair or replacement based on the assessment of an authorized technician.',
    'لا يشمل الضمان الأضرار الناتجة عن سوء الاستخدام أو الكسر أو السوائل أو التوصيل الكهربائي الخاطئ أو التلاعب بالجهاز.',
    'Warranty does not cover damage caused by misuse, physical damage, liquids, incorrect electrical installation, or tampering with the unit.',
    'عند وجود مشكلة، أحضر الجهاز مع رمز الضمان إلى المحل أو تواصل معنا عبر واتساب.',
    'If a problem occurs, bring the unit together with your warranty code to the shop, or contact us on WhatsApp.',
    1
),
(
    '9b1f4a60-0001-4d10-9c5a-000000000002',
    'Battery limited warranty',
    'solar_battery',
    12,
    'يغطي الضمان عيوب التصنيع في البطارية ضمن الاستخدام الطبيعي وفق تعليمات التركيب والشحن الموصى بها.',
    'Warranty covers manufacturing defects in the battery under normal use following the recommended installation and charging instructions.',
    'لا يشمل الضمان التفريغ العميق المتكرر أو التوصيل الخاطئ أو أضرار السوائل أو الكسر الخارجي.',
    'Warranty does not cover repeated deep discharge, incorrect wiring, liquid damage, or external physical damage.',
    'أحضر البطارية مع رمز الضمان إلى المحل ليتم فحصها من قبل الفني.',
    'Bring the battery with your warranty code to the shop for inspection by our technician.',
    2
),
(
    '9b1f4a60-0002-4d10-9c5a-000000000001',
    'Phone standard',
    'mobile_electronics',
    12,
    'يشمل الضمان عيوب التصنيع في الجهاز عند الاستخدام الطبيعي، ولا يشمل الملحقات المرفقة إلا إذا ذكر ذلك.',
    'Warranty covers manufacturing defects in the device under normal use. Included accessories are not covered unless stated.',
    'لا يشمل الضمان كسر الشاشة أو أضرار السوائل أو فتح الجهاز خارج الصيانة المعتمدة أو مشاكل السوفتوير الناتجة عن تطبيقات خارجية.',
    'Warranty does not cover screen breakage, liquid damage, opening the device outside authorized service, or software issues caused by third-party apps.',
    'عند وجود مشكلة، أحضر الجهاز مع رمز الضمان إلى المحل وسنقوم بفحصه.',
    'If a problem occurs, bring the device with your warranty code to the shop and we will inspect it.',
    1
),
(
    '9b1f4a60-0002-4d10-9c5a-000000000002',
    'Accessory limited',
    'mobile_electronics',
    6,
    'ضمان محدود يغطي عيوب التصنيع في الملحق عند الاستخدام الطبيعي.',
    'Limited warranty covering manufacturing defects in the accessory under normal use.',
    'لا يشمل الضمان الكسر أو أضرار السوائل أو التلف الناتج عن الاستخدام الخاطئ.',
    'Warranty does not cover physical damage, liquid damage, or damage caused by improper use.',
    'أحضر الملحق مع رمز الضمان إلى المحل خلال فترة الضمان.',
    'Bring the accessory with your warranty code to the shop within the warranty period.',
    2
),
(
    '9b1f4a60-0003-4d10-9c5a-000000000001',
    'Appliance standard',
    'appliances',
    24,
    'يشمل الضمان عيوب التصنيع والأعطال الفنية في الجهاز عند الاستخدام المنزلي الطبيعي.',
    'Warranty covers manufacturing defects and technical faults in the appliance under normal household use.',
    'لا يشمل الضمان سوء الاستخدام أو الكسر أو أضرار السوائل أو الأعطال الناتجة عن تذبذب الكهرباء دون منظم.',
    'Warranty does not cover misuse, physical damage, liquid damage, or faults caused by power fluctuations without a stabilizer.',
    'تواصل مع المحل مع ذكر رمز الضمان لترتيب الفحص أو الصيانة.',
    'Contact the shop and mention your warranty code to arrange inspection or service.',
    1
),
(
    '9b1f4a60-0004-4d10-9c5a-000000000001',
    'Tools limited',
    'furniture_tools',
    12,
    'ضمان محدود يغطي عيوب التصنيع في العدة عند الاستخدام الصحيح حسب تعليمات الشركة المصنعة.',
    'Limited warranty covering manufacturing defects in the tool under correct use per the manufacturer instructions.',
    'لا يشمل الضمان الاستهلاك الطبيعي للقطع أو الكسر أو الاستخدام التجاري المفرط أو أضرار السوائل.',
    'Warranty does not cover normal wear of consumable parts, physical damage, excessive commercial use, or liquid damage.',
    'أحضر العدة مع رمز الضمان إلى المحل ليتم فحصها.',
    'Bring the tool with your warranty code to the shop for inspection.',
    1
);
