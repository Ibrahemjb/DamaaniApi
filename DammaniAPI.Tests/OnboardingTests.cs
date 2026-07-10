using System.Net;
using System.Net.Http.Json;
using DammaniAPI.Features.Onboarding;
using Xunit;

namespace DammaniAPI.Tests;

public class OnboardingValidatorTests
{
    [Fact]
    public void SaveShopIdentityValidator_RequiresName()
    {
        var result = new SaveShopIdentity.CommandValidator().Validate(new SaveShopIdentity.Command());

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveShopIdentity.Command.Name));
    }

    [Theory]
    [InlineData("food")]
    public void SaveShopIdentityValidator_RejectsUnknownBusinessCategory(string category)
    {
        var command = new SaveShopIdentity.Command { Name = "Shop", BusinessCategory = category };

        var result = new SaveShopIdentity.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.BusinessCategory));
    }

    [Fact]
    public void SelectCategoriesValidator_RequiresCategories()
    {
        var result = new SelectCategories.CommandValidator().Validate(new SelectCategories.Command());

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SelectCategories.Command.Categories));
    }

    [Theory]
    [InlineData("other")]
    [InlineData("food")]
    public void SelectCategoriesValidator_RejectsUnknownCategory(string category)
    {
        var command = new SelectCategories.Command { Categories = [category] };

        var result = new SelectCategories.CommandValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SaveDefaultTermsValidator_RequiresAtLeastOneField()
    {
        var result = new SaveDefaultTerms.CommandValidator().Validate(new SaveDefaultTerms.Command());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetOnboardingState_SuggestStep_ProgressesWithShopData()
    {
        var fresh = new GetOnboardingState.QueryHandler.ShopRow { Name = "Shop" };
        Assert.Equal(1, GetOnboardingState.QueryHandler.SuggestStep(fresh, 0));

        var identityDone = new GetOnboardingState.QueryHandler.ShopRow { Name = "Shop", City = "Ramallah" };
        Assert.Equal(2, GetOnboardingState.QueryHandler.SuggestStep(identityDone, 0));

        var templates = new GetOnboardingState.QueryHandler.ShopRow { Name = "Shop", City = "Ramallah" };
        Assert.Equal(3, GetOnboardingState.QueryHandler.SuggestStep(templates, 2));

        var completed = new GetOnboardingState.QueryHandler.ShopRow
        {
            Name = "Shop",
            OnboardingCompletedAt = DateTime.UtcNow
        };
        Assert.Equal(4, GetOnboardingState.QueryHandler.SuggestStep(completed, 2));
    }
}

public class UploadLogoValidationTests
{
    [Fact]
    public void RejectsOversizedFile()
    {
        var file = ValidPng();
        file.Content = new byte[UploadLogo.CommandHandler.MaxFileSizeBytes + 1];

        Assert.False(UploadLogo.CommandHandler.IsValidFile(file));
    }

    [Fact]
    public void RejectsMismatchedExtension()
    {
        var file = ValidPng();
        file.FileName = "logo.svg";

        Assert.False(UploadLogo.CommandHandler.IsValidFile(file));
    }

    [Fact]
    public void AcceptsValidPng()
        => Assert.True(UploadLogo.CommandHandler.IsValidFile(ValidPng()));

    private static UploadLogo.FilePayload ValidPng() => new()
    {
        FileName = "logo.png",
        ContentType = "image/png",
        Content = [0x89, 0x50, 0x4E, 0x47]
    };
}

public class OnboardingEndpointAuthTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public OnboardingEndpointAuthTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetState_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/onboarding/getState");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/onboarding/saveShopIdentity")]
    [InlineData("/onboarding/selectCategories")]
    [InlineData("/onboarding/saveDefaultTerms")]
    [InlineData("/onboarding/complete")]
    public async Task WriteEndpoints_WithoutToken_Return401(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
