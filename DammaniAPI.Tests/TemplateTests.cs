using System.Net;
using System.Net.Http.Json;
using DammaniAPI.Features.Templates;
using Xunit;

namespace DammaniAPI.Tests;

public class TemplateValidatorTests
{
    private static CreateTemplate.Command ValidCreate() => new()
    {
        Name = "Solar inverter standard",
        Category = "solar_battery",
        DurationMonths = 24
    };

    [Fact]
    public void CreateTemplateValidator_AcceptsValidCommand()
    {
        var result = new CreateTemplate.CommandValidator().Validate(ValidCreate());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateTemplateValidator_RequiresName(string? name)
    {
        var command = ValidCreate();
        command.Name = name ?? "";

        var result = new CreateTemplate.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Name));
    }

    [Fact]
    public void CreateTemplateValidator_RejectsNameOver120Chars()
    {
        var command = ValidCreate();
        command.Name = new string('x', 121);

        var result = new CreateTemplate.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Name));
    }

    [Theory]
    [InlineData("food")]
    [InlineData("")]
    public void CreateTemplateValidator_RejectsUnknownCategory(string category)
    {
        var command = ValidCreate();
        command.Category = category;

        var result = new CreateTemplate.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Category));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    [InlineData(-6)]
    public void CreateTemplateValidator_RejectsDurationOutOfBounds(int months)
    {
        var command = ValidCreate();
        command.DurationMonths = months;

        var result = new CreateTemplate.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.DurationMonths));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public void CreateTemplateValidator_AcceptsDurationBounds(int months)
    {
        var command = ValidCreate();
        command.DurationMonths = months;

        var result = new CreateTemplate.CommandValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateTemplateValidator_RequiresTemplateId()
    {
        var command = new UpdateTemplate.Command
        {
            Name = "Updated",
            Category = "appliances",
            DurationMonths = 12
        };

        var result = new UpdateTemplate.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.TemplateId));
    }

    [Fact]
    public void DuplicateTemplateValidator_RejectsLongNewName()
    {
        var command = new DuplicateTemplate.Command
        {
            TemplateId = "t1",
            NewName = new string('x', 121)
        };

        var result = new DuplicateTemplate.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.NewName));
    }

    [Fact]
    public void SetTemplateStatusValidator_RequiresTemplateId()
    {
        var result = new SetTemplateStatus.CommandValidator().Validate(new SetTemplateStatus.Command());

        Assert.Contains(result.Errors, e => e.PropertyName == "TemplateId");
    }
}

public class TemplateEndpointAuthTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public TemplateEndpointAuthTests(ApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("/templates/getTemplates")]
    [InlineData("/templates/getTemplate?templateId=t1")]
    public async Task GetEndpoints_WithoutToken_Return401(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/templates/createTemplate")]
    [InlineData("/templates/updateTemplate")]
    [InlineData("/templates/duplicateTemplate")]
    [InlineData("/templates/setTemplateStatus")]
    public async Task WriteEndpoints_WithoutToken_Return401(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
