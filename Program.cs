using DammaniAPI.Database;
using DammaniAPI.Middlewares;
using DammaniAPI.Middlewares.Authentication;
using DammaniAPI.Utilities;
using Dapper;
using DotNetEnv;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;

if (File.Exists(".env"))
    Env.Load(".env", new LoadOptions(clobberExistingVars: false));

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Host.UseSerilog((_, config) => config.WriteTo.Console());

builder.Services.AddControllers();
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
