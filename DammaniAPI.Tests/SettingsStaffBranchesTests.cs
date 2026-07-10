using DammaniAPI.Features.Messaging;
using DammaniAPI.Features.Settings;
using DammaniAPI.Features.Staff;
using DammaniAPI.Features.Branches;
using Xunit;

namespace DammaniAPI.Tests;

public class SettingsValidatorTests
{
    [Fact]
    public void UpdateShopProfile_RequiresName()
    {
        var result = new UpdateShopProfile.CommandValidator().Validate(new UpdateShopProfile.Command());
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateShopProfile.Command.Name));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    public void UpdatePublicPageSettings_RejectsInvalidLanguage(string language)
    {
        var command = new UpdatePublicPageSettings.Command { PublicLanguage = language };
        var result = new UpdatePublicPageSettings.CommandValidator().Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateNotificationSettings_RequiresEmailWhenEnabled()
    {
        var command = new UpdateNotificationSettings.Command { EmailAlertsEnabled = true };
        var result = new UpdateNotificationSettings.CommandValidator().Validate(command);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void UpdateWarrantySettings_RejectsInvalidDuration(int months)
    {
        var command = new UpdateWarrantySettings.Command { DefaultWarrantyDurationMonths = months };
        var result = new UpdateWarrantySettings.CommandValidator().Validate(command);
        Assert.False(result.IsValid);
    }
}

public class MessageTemplateValidatorTests
{
    [Fact]
    public void UpdateMessageTemplate_RejectsUnknownKey()
    {
        var command = new UpdateMessageTemplate.Command { TemplateKey = "bogus", TextAr = "x" };
        var result = new UpdateMessageTemplate.CommandValidator().Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FindUnknown_DetectsBadVariable()
    {
        Assert.Equal("bad_var", MessageTemplateVars.FindUnknown("Hello {bad_var}"));
        Assert.Null(MessageTemplateVars.FindUnknown("Hello {customer_name}"));
    }
}

public class StaffValidatorTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("a@b.com", "+970599")]
    [InlineData("", "")]
    public void InviteStaff_RequiresEmailXorPhone(string? email, string? phone)
    {
        Assert.False(InviteStaff.CommandValidator.HasEmailXorPhone(email, phone));
    }

    [Theory]
    [InlineData("a@b.com", null)]
    [InlineData(null, "+970599")]
    public void InviteStaff_AcceptsEmailXorPhone(string? email, string? phone)
    {
        Assert.True(InviteStaff.CommandValidator.HasEmailXorPhone(email, phone));
    }
}

public class BranchValidatorTests
{
    [Fact]
    public void CreateBranch_RequiresName()
    {
        var result = new CreateBranch.CommandValidator().Validate(new CreateBranch.Command());
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateBranch.Command.Name));
    }
}
