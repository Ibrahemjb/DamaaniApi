using DammaniAPI.Features.Messaging;
using DammaniAPI.Features.Warranties;
using Xunit;

namespace DammaniAPI.Tests;

public class CreateWarrantyValidatorTests
{
    private static CreateWarranty.Command ValidCreate() => new()
    {
        CustomerName = "Ahmad Saleh",
        CustomerPhone = "+970 59 221 4810",
        ProductName = "Growatt 5KW Inverter",
        PurchaseDate = DateTime.UtcNow.Date,
        DurationMonths = 24
    };

    [Fact]
    public void AcceptsValidCommand()
    {
        var result = new CreateWarranty.CommandValidator().Validate(ValidCreate());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    public void RejectsMissingOrMalformedPhone(string phone)
    {
        var command = ValidCreate();
        command.CustomerPhone = phone;

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.CustomerPhone));
    }

    [Fact]
    public void RequiresPurchaseDateAndDurationForNonDraft()
    {
        var command = ValidCreate();
        command.PurchaseDate = null;
        command.DurationMonths = null;

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.PurchaseDate));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.DurationMonths));
    }

    [Fact]
    public void DraftAllowsMissingPurchaseDateAndDuration()
    {
        var command = ValidCreate();
        command.IsDraft = true;
        command.PurchaseDate = null;
        command.DurationMonths = null;

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DraftStillRequiresNamePhoneProduct()
    {
        var command = new CreateWarranty.Command { IsDraft = true };

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.CustomerName));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.CustomerPhone));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ProductName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void RejectsDurationOutOfRange(int months)
    {
        var command = ValidCreate();
        command.DurationMonths = months;

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.DurationMonths));
    }

    [Fact]
    public void RejectsPurchaseDateFarInFuture()
    {
        var command = ValidCreate();
        command.PurchaseDate = DateTime.UtcNow.Date.AddDays(31);

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.PurchaseDate));
    }

    [Fact]
    public void RejectsUnknownCategory()
    {
        var command = ValidCreate();
        command.Category = "food";

        var result = new CreateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Category));
    }
}

public class WarrantyCodeAndSlugTests
{
    [Fact]
    public void FormatCode_PadsSequenceToFourDigits()
    {
        Assert.Equal("DM-2607-0005", CreateWarranty.CommandHandler.FormatCode("DM-2607-", 5));
        Assert.Equal("DM-2607-1184", CreateWarranty.CommandHandler.FormatCode("DM-2607-", 1184));
        Assert.Equal("DM-2607-10001", CreateWarranty.CommandHandler.FormatCode("DM-2607-", 10001));
    }

    [Fact]
    public void GeneratePublicSlug_IsLongRandomAndUrlSafe()
    {
        var slug = CreateWarranty.CommandHandler.GeneratePublicSlug();
        var other = CreateWarranty.CommandHandler.GeneratePublicSlug();

        Assert.True(slug.Length >= 16);
        Assert.Matches("^[a-z0-9]+$", slug);
        Assert.NotEqual(slug, other);
    }

    [Theory]
    [InlineData("+970 59-221 4810", "+970592214810")]
    [InlineData("0599 123 456", "0599123456")]
    [InlineData("(0)59-9123456", "0599123456")]
    public void NormalizePhone_KeepsDigitsAndLeadingPlus(string input, string expected)
    {
        Assert.Equal(expected, CreateWarranty.CommandHandler.NormalizePhone(input));
    }
}

public class UpdateWarrantyValidatorTests
{
    private static UpdateWarranty.Command ValidUpdate() => new()
    {
        WarrantyId = Guid.NewGuid().ToString(),
        CustomerName = "Ahmad Saleh",
        CustomerPhone = "0599123456",
        ProductName = "Inverter",
        PurchaseDate = DateTime.UtcNow.Date,
        DurationMonths = 12
    };

    [Fact]
    public void AcceptsValidCommand()
    {
        Assert.True(new UpdateWarranty.CommandValidator().Validate(ValidUpdate()).IsValid);
    }

    [Fact]
    public void ActivationRequiresPurchaseDateAndDuration()
    {
        var command = ValidUpdate();
        command.Activate = true;
        command.PurchaseDate = null;
        command.DurationMonths = null;

        var result = new UpdateWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.PurchaseDate));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.DurationMonths));
    }

    [Fact]
    public void BuildChangeLog_TracksOldNewForCriticalFields()
    {
        var current = new UpdateWarranty.CurrentRow
        {
            SerialNumber = "SN-1",
            PurchaseDate = new DateTime(2026, 1, 1),
            DurationMonths = 12,
            ExpiryDate = new DateTime(2027, 1, 1)
        };
        var command = ValidUpdate();
        command.PurchaseDate = new DateTime(2026, 1, 1);
        command.DurationMonths = 24;

        var json = UpdateWarranty.CommandHandler.BuildChangeLog(
            current, command, "SN-2", new DateTime(2028, 1, 1), activating: false);

        Assert.Contains("\"serialNumber\"", json);
        Assert.Contains("SN-1", json);
        Assert.Contains("SN-2", json);
        Assert.Contains("\"durationMonths\"", json);
        Assert.Contains("\"expiryDate\"", json);
        Assert.DoesNotContain("\"purchaseDate\"", json);
    }

    [Fact]
    public void BuildChangeLog_EmptyWhenNothingCriticalChanged()
    {
        var current = new UpdateWarranty.CurrentRow
        {
            SerialNumber = "SN-1",
            PurchaseDate = new DateTime(2026, 1, 1),
            DurationMonths = 12,
            ExpiryDate = new DateTime(2027, 1, 1)
        };
        var command = ValidUpdate();
        command.PurchaseDate = new DateTime(2026, 1, 1);
        command.DurationMonths = 12;

        var json = UpdateWarranty.CommandHandler.BuildChangeLog(
            current, command, "SN-1", new DateTime(2027, 1, 1), activating: false);

        Assert.Equal("{}", json);
    }
}

public class CancelWarrantyValidatorTests
{
    [Fact]
    public void AcceptsKnownReasonCode()
    {
        var command = new CancelWarranty.Command
        {
            WarrantyId = Guid.NewGuid().ToString(),
            ReasonCode = "customer_return"
        };

        Assert.True(new CancelWarranty.CommandValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("because")]
    public void RejectsUnknownReasonCode(string reasonCode)
    {
        var command = new CancelWarranty.Command
        {
            WarrantyId = Guid.NewGuid().ToString(),
            ReasonCode = reasonCode
        };

        var result = new CancelWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ReasonCode));
    }

    [Fact]
    public void OtherReasonRequiresFreeText()
    {
        var command = new CancelWarranty.Command
        {
            WarrantyId = Guid.NewGuid().ToString(),
            ReasonCode = "other"
        };

        var result = new CancelWarranty.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ReasonText));
    }
}

public class WarrantyListFilterTests
{
    [Fact]
    public void AlwaysFiltersByShopFirst()
    {
        var (where, _) = WarrantyListFilter.Build("shop-1", new WarrantyListFilter.Args());

        Assert.StartsWith("w.ShopId = @ShopId", where);
    }

    [Fact]
    public void SearchMatchesCodeSerialNameProductAndPhoneDigits()
    {
        var (where, _) = WarrantyListFilter.Build("shop-1", new WarrantyListFilter.Args
        {
            Search = "059 9123"
        });

        Assert.Contains("w.Code LIKE @SearchPrefix", where);
        Assert.Contains("w.SerialNumber LIKE @SearchPrefix", where);
        Assert.Contains("c.Name LIKE @SearchContains", where);
        Assert.Contains("w.ProductName LIKE @SearchContains", where);
        Assert.Contains("c.Phone LIKE @SearchPhone", where);
    }

    [Fact]
    public void SearchWithoutDigitsSkipsPhoneClause()
    {
        var (where, _) = WarrantyListFilter.Build("shop-1", new WarrantyListFilter.Args
        {
            Search = "Ahmad"
        });

        Assert.DoesNotContain("@SearchPhone", where);
    }

    [Fact]
    public void ExpiredStatusUsesDerivedDateLogic()
    {
        var (where, _) = WarrantyListFilter.Build("shop-1", new WarrantyListFilter.Args
        {
            Status = "expired"
        });

        Assert.Contains("w.Status = 'active' AND w.ExpiryDate IS NOT NULL AND w.ExpiryDate < CURDATE()", where);
    }

    [Fact]
    public void ActiveStatusExcludesExpired()
    {
        var (where, _) = WarrantyListFilter.Build("shop-1", new WarrantyListFilter.Args
        {
            Status = "active"
        });

        Assert.Contains("w.ExpiryDate IS NULL OR w.ExpiryDate >= CURDATE()", where);
    }

    [Fact]
    public void EscapeLike_EscapesWildcards()
    {
        Assert.Equal("50\\%", WarrantyListFilter.EscapeLike("50%"));
        Assert.Equal("a\\_b", WarrantyListFilter.EscapeLike("a_b"));
    }

    [Fact]
    public void AllFiltersCombine()
    {
        var (where, _) = WarrantyListFilter.Build("shop-1", new WarrantyListFilter.Args
        {
            Search = "x",
            Status = "draft",
            Category = "solar_battery",
            CreatedFrom = DateTime.UtcNow.AddDays(-30),
            CreatedTo = DateTime.UtcNow,
            ExpiryFrom = DateTime.UtcNow,
            ExpiryTo = DateTime.UtcNow.AddYears(2),
            BranchId = "b1",
            CreatedByUserId = "u1"
        });

        Assert.Contains("w.Status = @Status", where);
        Assert.Contains("w.Category = @Category", where);
        Assert.Contains("w.CreatedAt >= @CreatedFrom", where);
        Assert.Contains("w.CreatedAt < @CreatedTo", where);
        Assert.Contains("w.ExpiryDate >= @ExpiryFrom", where);
        Assert.Contains("w.ExpiryDate <= @ExpiryTo", where);
        Assert.Contains("w.BranchId = @BranchId", where);
        Assert.Contains("w.CreatedByUserId = @CreatedByUserId", where);
    }
}

public class ExportCsvTests
{
    [Fact]
    public void Escape_QuotesCommasQuotesAndNewlines()
    {
        Assert.Equal("plain", ExportWarranties.QueryHandler.Escape("plain"));
        Assert.Equal("\"a,b\"", ExportWarranties.QueryHandler.Escape("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", ExportWarranties.QueryHandler.Escape("say \"hi\""));
        Assert.Equal("\"line1\nline2\"", ExportWarranties.QueryHandler.Escape("line1\nline2"));
        Assert.Equal("محول جروات", ExportWarranties.QueryHandler.Escape("محول جروات"));
    }
}

public class DefaultMessagesTests
{
    [Fact]
    public void AllTemplateKeysHaveBothLanguages()
    {
        string[] keys =
        [
            "warranty_created", "request_received", "status_reviewing", "status_waiting_customer",
            "status_sent_supplier", "status_repaired", "status_replaced", "status_rejected", "status_closed"
        ];
        foreach (var key in keys)
        {
            Assert.True(DefaultMessages.Defaults.ContainsKey(key), $"missing {key}");
            Assert.False(string.IsNullOrWhiteSpace(DefaultMessages.Defaults[key].Ar), $"{key} ar empty");
            Assert.False(string.IsNullOrWhiteSpace(DefaultMessages.Defaults[key].En), $"{key} en empty");
        }
    }

    [Fact]
    public void WarrantyCreatedContainsAllVariables()
    {
        var text = DefaultMessages.Defaults["warranty_created"];
        string[] variables = ["{customer_name}", "{product_name}", "{warranty_code}", "{expiry_date}", "{public_link}", "{shop_name}"];
        foreach (var variable in variables)
        {
            Assert.Contains(variable, text.Ar);
            Assert.Contains(variable, text.En);
        }
    }
}
