using Dapper;
using DammaniAPI.Features;
using DammaniAPI.Features.Admin;
using DammaniAPI.Services.Auth;
using Serilog;

namespace DammaniAPI.Database;

/// <summary>
/// Local/demo seed for platform admin console. Gated by SEED_DEMO_DATA=true.
/// Creates admin@damaani.local / Admin123! and sample shops when the admin user is missing.
/// </summary>
public static class AdminDemoSeedProvisioner
{
    public const string AdminEmail = "admin@damaani.local";
    public const string AdminPassword = "Admin123!";

    public static async Task RunAsync(IManagementDatabase mdb, IPasswordHasher passwordHasher, bool enabled)
    {
        if (!enabled)
            return;

        using var db = mdb.Open();
        var existingAdmin = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM User WHERE LOWER(Email) = @Email",
            new { Email = AdminEmail });

        if (existingAdmin > 0)
        {
            Log.Information("SEED_DEMO_DATA: demo admin already present (idempotent).");
            return;
        }

        using var tx = db.BeginTransaction();
        try
        {
            var adminId = "a0000000-0001-4000-8000-000000000001";
            await db.ExecuteAsync(
                """
                INSERT INTO User
                    (Id, Email, FullName, Phone, PasswordHash, Language, Status, IsPlatformAdmin, AdminRole, EmailVerifiedAt, CreatedAt, UpdatedAt)
                VALUES
                    (@Id, @Email, 'Platform Admin', '+970500000001', @Hash, 'en', 'active', 1, @Role, UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP())
                """,
                new
                {
                    Id = adminId,
                    Email = AdminEmail,
                    Hash = passwordHasher.Hash(AdminPassword),
                    Role = AdminRoles.Super
                },
                tx);

            var freePlanId = await db.ExecuteScalarAsync<string>(
                "SELECT Id FROM Plan WHERE Code = 'free' LIMIT 1", tx);
            var starterPlanId = await db.ExecuteScalarAsync<string>(
                "SELECT Id FROM Plan WHERE Code = 'starter' LIMIT 1", tx);
            var proPlanId = await db.ExecuteScalarAsync<string>(
                "SELECT Id FROM Plan WHERE Code = 'pro' LIMIT 1", tx);
            var businessPlanId = await db.ExecuteScalarAsync<string>(
                "SELECT Id FROM Plan WHERE Code = 'business' LIMIT 1", tx);

            var shops = new[]
            {
                new SeedShop("b0000001-0001-4000-8000-000000000001", "Al Noor Solar", "Ramallah", starterPlanId!, "active", null, 184, false),
                new SeedShop("b0000002-0001-4000-8000-000000000002", "City Mobile", "Nablus", freePlanId!, "active", null, 30, true),
                new SeedShop("b0000003-0001-4000-8000-000000000003", "Green Power Co", "Hebron", proPlanId!, "active", null, 420, false),
                new SeedShop("b0000004-0001-4000-8000-000000000004", "Battery Hub", "Gaza", freePlanId!, "suspended", "Suspicious high-volume free usage", 30, false),
                new SeedShop("b0000005-0001-4000-8000-000000000005", "SunTech Palestine", "Jenin", businessPlanId!, "active", null, 1100, false),
                new SeedShop("b0000006-0001-4000-8000-000000000006", "Inverter Plus", "Bethlehem", starterPlanId!, "active", null, 290, true),
                new SeedShop("b0000007-0001-4000-8000-000000000007", "Home Appliances PS", "Tulkarm", freePlanId!, "active", null, 12, false),
                new SeedShop("b0000008-0001-4000-8000-000000000008", "Power Tools Center", "Qalqilya", starterPlanId!, "active", null, 95, false),
                new SeedShop("b0000009-0001-4000-8000-000000000009", "Solar Valley", "Jericho", proPlanId!, "active", null, 800, false),
                new SeedShop("b0000010-0001-4000-8000-000000000010", "Quick Fix Electronics", "Salfit", freePlanId!, "active", null, 28, false),
            };

            for (var i = 0; i < shops.Length; i++)
            {
                var shop = shops[i];
                var ownerId = $"c00000{(i + 1):D2}-0001-4000-8000-0000000000{(i + 1):D2}";

                await db.ExecuteAsync(
                    """
                    INSERT INTO User
                        (Id, Email, FullName, Phone, PasswordHash, Language, Status, IsPlatformAdmin, AdminRole, EmailVerifiedAt, CreatedAt, UpdatedAt)
                    VALUES
                        (@Id, @Email, @Name, @Phone, @Hash, 'ar', 'active', 0, NULL, UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP())
                    """,
                    new
                    {
                        Id = ownerId,
                        Email = $"owner{i + 1}@demo.damaani.local",
                        Name = $"{shop.Name} Owner",
                        Phone = $"+97059{1000000 + i}",
                        Hash = passwordHasher.Hash("Owner123!")
                    },
                    tx);

                await db.ExecuteAsync(
                    """
                    INSERT INTO Shop
                        (Id, Name, Phone, WhatsAppNumber, City, Country, BusinessCategory, Status, SuspensionNote, OnboardingCompletedAt, CreatedAt, UpdatedAt)
                    VALUES
                        (@Id, @Name, @Phone, @Phone, @City, 'PS', 'solar', @Status, @Note, UTC_TIMESTAMP(), DATE_SUB(UTC_TIMESTAMP(), INTERVAL @Days DAY), UTC_TIMESTAMP())
                    """,
                    new
                    {
                        shop.Id,
                        shop.Name,
                        Phone = $"+9702{2000000 + i}",
                        shop.City,
                        Status = shop.Status,
                        Note = shop.SuspensionNote,
                        Days = 40 - i * 3
                    },
                    tx);

                await db.ExecuteAsync(
                    """
                    INSERT INTO ShopUser (Id, ShopId, UserId, Role, Status, CreatedAt)
                    VALUES (@Id, @ShopId, @UserId, 'owner', 'active', UTC_TIMESTAMP())
                    """,
                    new { Id = Guid.NewGuid().ToString(), ShopId = shop.Id, UserId = ownerId },
                    tx);

                var periodEnd = shop.PendingUpgrade
                    ? DateTime.UtcNow.Date.AddDays(12)
                    : DateTime.UtcNow.Date.AddDays(20 + i);
                var cancelAtEnd = i == 5;
                var scheduledPlanId = shop.PendingUpgrade ? proPlanId : null;

                await db.ExecuteAsync(
                    """
                    INSERT INTO Subscription
                        (Id, ShopId, PlanId, Status, CurrentPeriodStart, CurrentPeriodEnd, ScheduledPlanId, CancelAtPeriodEnd, CreatedAt, UpdatedAt)
                    VALUES
                        (@Id, @ShopId, @PlanId, 'active', UTC_DATE(), @PeriodEnd, @Scheduled, @Cancel, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                    """,
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        ShopId = shop.Id,
                        PlanId = shop.PlanId,
                        PeriodEnd = periodEnd,
                        Scheduled = scheduledPlanId,
                        Cancel = cancelAtEnd ? 1 : 0
                    },
                    tx);

                if (shop.PlanId != freePlanId)
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO Payment
                            (Id, ShopId, SubscriptionId, PlanId, AmountUsd, AmountIls, Method, Status, Reference, PaidAt, CreatedAt)
                        SELECT
                            @PayId, @ShopId, sub.Id, @PlanId, p.PriceUsd, p.PriceIls, 'manual', 'paid', @Ref, UTC_TIMESTAMP(), UTC_TIMESTAMP()
                        FROM Subscription sub
                        JOIN Plan p ON p.Id = @PlanId
                        WHERE sub.ShopId = @ShopId
                        LIMIT 1
                        """,
                        new
                        {
                            PayId = Guid.NewGuid().ToString(),
                            ShopId = shop.Id,
                            PlanId = shop.PlanId,
                            Ref = $"SEED-{i + 1:D3}"
                        },
                        tx);
                }

                var customerId = Guid.NewGuid().ToString();
                await db.ExecuteAsync(
                    """
                    INSERT INTO Customer (Id, ShopId, Name, Phone, City, CreatedAt)
                    VALUES (@Id, @ShopId, @Name, @Phone, @City, UTC_TIMESTAMP())
                    """,
                    new
                    {
                        Id = customerId,
                        ShopId = shop.Id,
                        Name = $"Customer {i + 1}",
                        Phone = $"+97059{2000000 + i}",
                        shop.City
                    },
                    tx);

                var warrantyCount = Math.Min(shop.UsedCards, 8);
                for (var w = 0; w < warrantyCount; w++)
                {
                    var warrantyId = Guid.NewGuid().ToString();
                    var code = $"W-SEED{i + 1:D2}{w + 1:D2}";
                    var slug = $"seed{i + 1:D2}w{w + 1:D2}abcdefgh";
                    await db.ExecuteAsync(
                        """
                        INSERT INTO Warranty
                            (Id, ShopId, CustomerId, Code, PublicSlug, ProductName, Category, SerialNumber,
                             PurchaseDate, DurationMonths, ExpiryDate, TermsEn, Status, CreatedByUserId, CreatedAt)
                        VALUES
                            (@Id, @ShopId, @CustomerId, @Code, @Slug, @Product, 'solar', @Serial,
                             UTC_DATE(), 12, DATE_ADD(UTC_DATE(), INTERVAL 12 MONTH), 'Demo terms', 'active', @OwnerId, DATE_SUB(UTC_TIMESTAMP(), INTERVAL @Hours HOUR))
                        """,
                        new
                        {
                            Id = warrantyId,
                            ShopId = shop.Id,
                            CustomerId = customerId,
                            Code = code,
                           Slug = slug[..Math.Min(24, slug.Length)],
                            Product = $"Inverter {w + 1}",
                            Serial = $"SN-SEED-{i + 1}-{w + 1}",
                            OwnerId = ownerId,
                            Hours = w + 1
                        },
                        tx);

                    if (w == 0)
                    {
                        await db.ExecuteAsync(
                            """
                            INSERT INTO ServiceRequest
                                (Id, ShopId, WarrantyId, RequestNumber, CustomerName, CustomerPhone, ProblemType, Description, Status, Source, CreatedAt)
                            VALUES
                                (@Id, @ShopId, @WarrantyId, @Num, @CustName, @CustPhone, 'not_working', 'Demo service request', 'new', 'public', UTC_TIMESTAMP())
                            """,
                            new
                            {
                                Id = Guid.NewGuid().ToString(),
                                ShopId = shop.Id,
                                WarrantyId = warrantyId,
                                Num = $"SR-{DateTime.UtcNow:yyMM}-{i + 1:D4}",
                                CustName = $"Customer {i + 1}",
                                CustPhone = $"+97059{2000000 + i}"
                            },
                            tx);
                    }
                }

                // Pad usage count with extra warranties created this month (metadata only for meter).
                for (var extra = warrantyCount; extra < Math.Min(shop.UsedCards, 40); extra++)
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO Warranty
                            (Id, ShopId, CustomerId, Code, PublicSlug, ProductName, Category, SerialNumber,
                             PurchaseDate, DurationMonths, ExpiryDate, TermsEn, Status, CreatedByUserId, CreatedAt)
                        VALUES
                            (@Id, @ShopId, @CustomerId, @Code, @Slug, @Product, 'solar', @Serial,
                             UTC_DATE(), 12, DATE_ADD(UTC_DATE(), INTERVAL 12 MONTH), 'Demo terms', 'active', @OwnerId, UTC_TIMESTAMP())
                        """,
                        new
                        {
                            Id = Guid.NewGuid().ToString(),
                            ShopId = shop.Id,
                            CustomerId = customerId,
                            Code = $"W-PAD{i + 1:D2}{extra + 1:D3}",
                            Slug = $"pad{i + 1:D2}{extra + 1:D3}{Guid.NewGuid():N}"[..24],
                            Product = $"Panel {extra + 1}",
                            Serial = $"SN-PAD-{i + 1}-{extra + 1}",
                            OwnerId = ownerId
                        },
                        tx);
                }
            }

            await db.ExecuteAsync(
                """
                INSERT INTO ContactMessage (Id, Name, Email, Topic, Message, Status, CreatedAt)
                VALUES
                    (@Id1, 'Ahmad Saleh', 'ahmad@example.com', 'billing', 'Need help confirming bank transfer for Starter plan.', 'unread', UTC_TIMESTAMP()),
                    (@Id2, 'Lina Omar', 'lina@example.com', 'support', 'Public warranty page shows unavailable for our shop.', 'in_progress', DATE_SUB(UTC_TIMESTAMP(), INTERVAL 1 DAY)),
                    (@Id3, 'Demo Shop', 'owner1@demo.damaani.local', 'feature', 'Can we export warranties to Excel?', 'closed', DATE_SUB(UTC_TIMESTAMP(), INTERVAL 3 DAY))
                """,
                new
                {
                    Id1 = Guid.NewGuid().ToString(),
                    Id2 = Guid.NewGuid().ToString(),
                    Id3 = Guid.NewGuid().ToString()
                },
                tx);

            await db.ExecuteAsync(
                """
                INSERT INTO ActivityLog (Id, ShopId, EntityType, EntityId, Action, ActorUserId, Details, CreatedAt)
                VALUES
                    (@Id1, @Shop4, 'shop', @Shop4, 'shop.suspended', @AdminId, '{"note":"Suspicious high-volume free usage"}', UTC_TIMESTAMP()),
                    (@Id2, @Shop1, 'shop', @Shop1, 'plan.changed', @AdminId, '{"note":"Seed payment confirmed"}', DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 DAY))
                """,
                new
                {
                    Id1 = Guid.NewGuid().ToString(),
                    Id2 = Guid.NewGuid().ToString(),
                    Shop4 = shops[3].Id,
                    Shop1 = shops[0].Id,
                    AdminId = adminId
                },
                tx);

            tx.Commit();
            Log.Information(
                "SEED_DEMO_DATA: seeded platform admin {Email} / {Password} and {Count} demo shops.",
                AdminEmail, AdminPassword, shops.Length);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private sealed record SeedShop(
        string Id,
        string Name,
        string City,
        string PlanId,
        string Status,
        string? SuspensionNote,
        int UsedCards,
        bool PendingUpgrade);
}
