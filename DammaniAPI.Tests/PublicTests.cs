using System.Reflection;
using DammaniAPI.Features.Public;
using Xunit;

namespace DammaniAPI.Tests;

// DMN-501 leak-contract test: the public warranty response is a pinned,
// explicit whitelist. Adding ANY property to these types fails this test on
// purpose — widen the pin only after confirming the field is customer-safe
// (BP §10.15/§14: no customer identity, no shop dashboard data).
public class PublicWarrantyContractTests
{
    private static string[] PropertyNames(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).OrderBy(n => n).ToArray();

    [Fact]
    public void ResultExposesOnlyWhitelistedFields()
    {
        Assert.Equal(
            new[] { "ErrorCode", "Shop", "Success", "Warranty" },
            PropertyNames(typeof(GetPublicWarranty.Result)));

        Assert.Equal(
            new[]
            {
                "AllowExpiredRequests", "City", "DamaaniBranding", "LogoUrl", "Name",
                "PublicLanguage", "ShowAddress", "ShowWhatsApp", "WhatsAppNumber"
            },
            PropertyNames(typeof(GetPublicWarranty.ShopInfo)));

        Assert.Equal(
            new[]
            {
                "Code", "ExpiryDate", "Model", "ProductName", "PurchaseDate",
                "SerialNumber", "Status", "TermsAr", "TermsEn"
            },
            PropertyNames(typeof(GetPublicWarranty.WarrantyInfo)));
    }

    [Fact]
    public void ResultContainsNoCustomerIdentityOrInternalFields()
    {
        var allNames = new[]
            {
                typeof(GetPublicWarranty.Result),
                typeof(GetPublicWarranty.ShopInfo),
                typeof(GetPublicWarranty.WarrantyInfo)
            }
            .SelectMany(PropertyNames)
            .ToArray();

        string[] forbidden =
        {
            "CustomerName", "CustomerPhone", "CustomerId", "Customer",
            "PasswordHash", "Email", "CancelReason", "Notes", "Address",
            "ShopId", "Id", "CreatedByUserId", "SuspensionNote"
        };
        foreach (var name in forbidden)
            Assert.DoesNotContain(name, allNames);
    }
}

public class SubmitServiceRequestValidatorTests
{
    private static SubmitServiceRequest.Command ValidCommand() => new()
    {
        Slug = "abc123def456ghi789jk",
        CustomerName = "Ahmad Saleh",
        CustomerPhone = "+970 59 221 4810",
        ProblemType = "not_working",
        Description = "The inverter stopped working after a power cut.",
        PreferredContact = "whatsapp",
        Consent = true
    };

    [Fact]
    public void AcceptsValidCommand()
    {
        var result = new SubmitServiceRequest.CommandValidator().Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiresConsent()
    {
        var command = ValidCommand();
        command.Consent = false;

        var result = new SubmitServiceRequest.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Consent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("exploded")]
    [InlineData("NOT A TYPE")]
    public void RejectsUnknownProblemType(string problemType)
    {
        var command = ValidCommand();
        command.ProblemType = problemType;

        var result = new SubmitServiceRequest.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ProblemType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    public void RejectsMissingOrMalformedPhone(string phone)
    {
        var command = ValidCommand();
        command.CustomerPhone = phone;

        var result = new SubmitServiceRequest.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.CustomerPhone));
    }

    [Fact]
    public void RejectsEmptyOrOversizedDescription()
    {
        var empty = ValidCommand();
        empty.Description = "";
        var oversized = ValidCommand();
        oversized.Description = new string('x', 4001);

        Assert.Contains(new SubmitServiceRequest.CommandValidator().Validate(empty).Errors,
            e => e.PropertyName == nameof(empty.Description));
        Assert.Contains(new SubmitServiceRequest.CommandValidator().Validate(oversized).Errors,
            e => e.PropertyName == nameof(oversized.Description));
    }

    [Fact]
    public void RejectsUnknownPreferredContact()
    {
        var command = ValidCommand();
        command.PreferredContact = "carrier_pigeon";

        var result = new SubmitServiceRequest.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.PreferredContact));
    }
}

public class ServiceRequestFileTests
{
    private static SubmitServiceRequest.FilePayload Jpeg(int size = 64)
    {
        var content = new byte[size];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        return new SubmitServiceRequest.FilePayload
        {
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Content = content
        };
    }

    [Fact]
    public void AcceptsValidJpegList()
    {
        Assert.True(SubmitServiceRequest.CommandHandler.FilesAreValid(new[] { Jpeg(), Jpeg(), Jpeg() }));
    }

    [Fact]
    public void RejectsMoreThanThreeFiles()
    {
        Assert.False(SubmitServiceRequest.CommandHandler.FilesAreValid(
            new[] { Jpeg(), Jpeg(), Jpeg(), Jpeg() }));
    }

    [Fact]
    public void RejectsOversizedFile()
    {
        var oversized = Jpeg(5 * 1024 * 1024 + 1);

        Assert.False(SubmitServiceRequest.CommandHandler.FilesAreValid(new[] { oversized }));
    }

    [Fact]
    public void RejectsDisallowedContentType()
    {
        var file = Jpeg();
        file.ContentType = "application/x-msdownload";

        Assert.False(SubmitServiceRequest.CommandHandler.FilesAreValid(new[] { file }));
    }

    [Fact]
    public void RejectsSpoofedImageContent()
    {
        // "MZ" executable header sent with an image content type.
        var spoofed = new SubmitServiceRequest.FilePayload
        {
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Content = [0x4D, 0x5A, 0x90, 0x00, 0x03]
        };

        Assert.False(SubmitServiceRequest.CommandHandler.FilesAreValid(new[] { spoofed }));
    }

    [Fact]
    public void RecognizesImageSignatures()
    {
        Assert.True(SubmitServiceRequest.CommandHandler.HasValidImageSignature(
            "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));
        Assert.True(SubmitServiceRequest.CommandHandler.HasValidImageSignature(
            "image/webp", "RIFF????WEBP"u8.ToArray()));
        Assert.True(SubmitServiceRequest.CommandHandler.HasValidImageSignature(
            "image/heic", [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63]));
        Assert.False(SubmitServiceRequest.CommandHandler.HasValidImageSignature(
            "image/png", [0xFF, 0xD8, 0xFF]));
    }
}

public class RequestNumberTests
{
    [Fact]
    public void FormatsGlobalMonthlySequence()
    {
        Assert.Equal("SR-2607-0001", SubmitServiceRequest.CommandHandler.FormatRequestNumber("SR-2607-", 1));
        Assert.Equal("SR-2607-0042", SubmitServiceRequest.CommandHandler.FormatRequestNumber("SR-2607-", 42));
        Assert.Equal("SR-2607-12345", SubmitServiceRequest.CommandHandler.FormatRequestNumber("SR-2607-", 12345));
    }
}
