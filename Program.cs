using DammaniAPI.Database;
using DammaniAPI.Middlewares;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using Dapper;
using DotNetEnv;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Serilog;

if (File.Exists(".env"))
    Env.Load(".env", new LoadOptions(clobberExistingVars: false));

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Host.UseSerilog((_, config) => config.WriteTo.Console());

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => ToCamelCasePath(item.Key),
                item => item.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        var details = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };

        return new BadRequestObjectResult(details);
    };
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddCors();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddResponseCompression();
builder.Services.AddSingleton<IManagementDatabase, ManagementDatabase>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

SqlMapper.AddTypeHandler(new UtcDateTimeHandler());

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseResponseCompression();
app.UseMiddleware<LoggingMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();
app.MapControllers();

try
{
    new DatabaseMigrator(app.Services.GetRequiredService<IManagementDatabase>()).Migrate();
}
catch (Exception ex)
{
    Log.Warning(ex, "Database migration skipped or failed — ensure MySQL is running and DB_CONNECTION_STRING is set");
}

app.Run(Environment.GetEnvironmentVariable("LISTENING_URL") ?? "http://0.0.0.0:5000");

static string ToCamelCasePath(string key)
{
    if (string.IsNullOrWhiteSpace(key))
        return key;

    return string.Join(".", key.Split('.').Select(part =>
        string.IsNullOrEmpty(part) || char.IsLower(part[0])
            ? part
            : char.ToLowerInvariant(part[0]) + part[1..]));
}

public partial class Program;
