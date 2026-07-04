using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DammaniAPI.Tests;

public class ApiContractTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsCamelCaseHealthyResponse()
    {
        var response = await _client.GetAsync("/health");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ValidationFailure_ReturnsValidationProblemDetailsWithCamelCaseFieldKeys()
    {
        var response = await _client.PostAsJsonAsync("/test-contract/validation", new { email = "" });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.True(json.GetProperty("errors").TryGetProperty("email", out var emailErrors));
        Assert.NotEmpty(emailErrors.EnumerateArray());
    }

    [Fact]
    public async Task UnhandledException_ReturnsStandardEnvelopeWithoutDetails()
    {
        var response = await _client.GetAsync("/test-contract/throw");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("internal_error", json.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }
}

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_CONNECTION_STRING"] = "Server=127.0.0.1;Port=1;Database=dammani_test;Uid=root;Pwd=test;",
                ["JWT_SECRET"] = "01234567890123456789012345678901",
                ["JWT_ISSUER"] = "damaani-api",
                ["JWT_APP_IDENTIFIER"] = "dammani-api"
            });
        });
        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(ContractTestController).Assembly);
            services.AddValidatorsFromAssemblyContaining<ContractTestController>();
        });
    }
}

[ApiController]
[Route("test-contract")]
public class ContractTestController : ControllerBase
{
    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("boom");

    [HttpPost("validation")]
    public IActionResult Validation([FromBody] SampleCommand command)
        => Ok(new { success = true, command.Email });

    public class SampleCommand
    {
        public string Email { get; set; } = "";
    }

    public class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(command => command.Email).NotEmpty().EmailAddress();
        }
    }
}
