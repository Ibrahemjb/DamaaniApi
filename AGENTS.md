# DammaniAPI — Agent Guide

.NET 8 HTTP API using **vertical slices**: thin controllers → MediatR handlers → Dapper SQL → MySQL. No EF, no repository layer.

---

## Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8 (`Microsoft.NET.Sdk.Web`) |
| HTTP | ASP.NET Core controllers (not minimal APIs) |
| Application | MediatR — one handler per use case |
| Data | Dapper — hand-written SQL |
| Migrations | DbUp + numbered SQL files in `Database/Scripts/` |
| Database | MySQL 8 (`MySql.Data`) |
| Validation | FluentValidation (auto-validation on commands) |
| Logging | Serilog (console) |
| Config | `.env` + environment variables |

---

## Folder Layout

```
DammaniAPI/
├── Program.cs
├── Controllers/              # HTTP only — bind request, call MediatR, return Ok()
├── Features/                 # Vertical slices — one file per use case
│   └── {Domain}/
│       └── {UseCaseName}.cs  # Query/Command + Handler + Result (+ Validator for commands)
├── Database/
│   ├── IManagementDatabase.cs
│   ├── ManagementDatabase.cs
│   ├── DatabaseMigrator.cs
│   └── Scripts/
│       ├── 00000_init.sql
│       └── 00001_*.sql       # incremental migrations
├── Middlewares/
│   ├── LoggingMiddleware.cs
│   └── Authentication/
├── Services/                 # external integrations (add when needed)
└── Utilities/                # Dapper helpers, HttpContext extensions
```

---

## Core Rules

1. **Controllers never touch SQL.** They bind input, optionally enrich from `HttpContext`, call `_mediator.Send()`, return `Ok(result)`.
2. **Features never know about HTTP routing.** No `IHttpContextAccessor` in handlers — pass enriched values from the controller.
3. **SQL lives in the handler.** Write Dapper queries directly inside `QueryHandler` / `CommandHandler`. Do not create entity files with static query methods.
4. **No shared models unless reused.** Only add a POCO under `Features/{Domain}/` when **two or more** features need the same shape. Otherwise map to primitives, anonymous results, or inline `Result` properties.
5. **One connection per handler:** `using var db = _mdb.Open();`
6. **No repository layer.** SQL stays in handlers (or shared only when truly duplicated).
7. **Keep diffs minimal.** Match existing naming, nesting, and file layout. Do not add abstractions, services, or helpers unless explicitly needed.

---

## Creating a New Feature

### 1. Pick the domain folder

Place the file under `Features/{Domain}/`, e.g. `Features/Users/`, `Features/Orders/`.

### 2. One file per use case

File name = use case name in PascalCase, e.g. `GetUser.cs`, `CreateOrder.cs`.

### 3. Naming convention

| Operation | MediatR type | Handler |
|-----------|--------------|---------|
| Read | `Query` | `QueryHandler` |
| Write | `Command` | `CommandHandler` |

Each file contains nested classes: the message, the handler, and a feature-specific `Result` (not a global `Result<T>`).

### 4. Query template (read)

```csharp
using Dapper;
using DammaniAPI.Database;
using MediatR;

namespace DammaniAPI.Features.Users;

public class GetUser
{
    public class Query : IRequest<Result>
    {
        public string? UserId { get; set; }
    }

    public class Result
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
    }

    public class QueryHandler : IRequestHandler<Query, Result>
    {
        private readonly IManagementDatabase _mdb;

        public QueryHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Query request, CancellationToken ct)
        {
            using var db = _mdb.Open();
            return await db.QueryFirstOrDefaultAsync<Result>(
                "SELECT Id, Email FROM User WHERE Id = @UserId",
                new { UserId = request.UserId }) ?? new Result();
        }
    }
}
```

### 5. Command template (write)

```csharp
using Dapper;
using DammaniAPI.Database;
using FluentValidation;
using MediatR;

namespace DammaniAPI.Features.Users;

public class CreateUser
{
    public class Command : IRequest<Result>
    {
        public string Email { get; set; } = "";
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? Id { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        private readonly IManagementDatabase _mdb;

        public CommandHandler(IManagementDatabase mdb) => _mdb = mdb;

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var id = Guid.NewGuid().ToString();
            using var db = _mdb.Open();
            await db.ExecuteAsync(
                "INSERT INTO User (Id, Email) VALUES (@Id, @Email)",
                new { Id = id, request.Email });
            return new Result { Success = true, Id = id };
        }
    }
}
```

### 6. Validation rules

- Add `AbstractValidator<Command>` in the **same file** as the command.
- Validate **commands** (writes). Queries are rarely validated unless input is complex.
- FluentValidation auto-validation is enabled in `Program.cs` — no manual `Validate()` calls in controllers.

### 7. Transactions

For multi-statement writes, use a transaction inside the handler:

```csharp
using var db = _mdb.Open();
using var tx = db.BeginTransaction();
try
{
    // multiple ExecuteAsync / Query calls with transaction: tx
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```

---

## Creating a Controller Endpoint

1. Add or extend a controller in `Controllers/`.
2. Route by resource: `[Route("users")]`, action in camelCase: `[HttpGet("checkIfUserExists")]`.
3. Bind directly to the MediatR message:

```csharp
[HttpGet("checkIfUserExists")]
public async Task<IActionResult> CheckIfUserExists([FromQuery] CheckIfUserExists.Query query)
    => Ok(await _mediator.Send(query));
```

4. Enrich from context in the controller (not middleware), e.g. `command.UserId = _context.CurrentUserId();`
5. Return `Ok(result)`. Handlers own the response shape (`success`, `errorCode`, etc.).

MediatR auto-registers handlers from the assembly — no manual DI registration per feature.

---

## Database Migrations

### Script location

`Database/Scripts/` — copied to output via `.csproj` (`CopyToOutputDirectory`).

### Naming

| File | Purpose |
|------|---------|
| `00000_init.sql` | Bootstrap schema (`CREATE TABLE IF NOT EXISTS …`) |
| `00001_add_foo.sql` | First incremental change |
| `00002_seed_bar.sql` | Next change |

- **Five-digit numeric prefix** determines execution order.
- Use descriptive snake_case suffix after the number.
- DbUp journals applied scripts in `{database}.schemaversions`.

### Two-phase migrate (startup)

`DatabaseMigrator` runs:

1. **Only** `00000_init.sql`
2. **All** scripts in the folder

This supports bootstrapping existing databases. Do not change this flow unless there is a strong reason.

### Rules when writing migrations

1. **Never edit a script that has already been applied** in any environment. Add a new numbered script instead.
2. Prefer idempotent DDL where practical: `CREATE TABLE IF NOT EXISTS`, `ALTER TABLE …` only when safe.
3. One logical change per file (one table, one index batch, one seed dataset).
4. Do not create the `schemaversions` table manually — DbUp manages it via `JournalToMySqlTable`.
5. Test locally: start the app (migrations run at startup) or fix connection string in `.env` first.

### Example incremental script (`00001_add_user_status.sql`)

```sql
ALTER TABLE User ADD COLUMN Status VARCHAR(32) NOT NULL DEFAULT 'active';
```

---

## SQL & Schema Conventions

- Table names: **PascalCase** (`User`, `Company`) — MySQL should run with `lower-case-table-names=1`.
- Parameterized queries only — always use `@ParamName`, never string interpolation for values.
- `DateTime` values are stored/read as UTC (`UtcDateTimeHandler` registered in `Program.cs`).
- `Utilities/DapperExtensions.cs` has `InsertDynamicAsync` / `UpdateDynamicAsync` for simple CRUD when a full command handler would be overkill — prefer explicit SQL in handlers.

---

## Middleware Pipeline

Order in `Program.cs`:

```
UseCors → UseResponseCompression → LoggingMiddleware → AuthenticationMiddleware → MapControllers
```

- **`/health`** is public (whitelisted in `AuthenticationMiddleware`).
- When `JWT_ISSUER_URL` is empty, auth is skipped (local dev).
- `HttpContext.Items["UserId"]` and `["Scope"]` are set by auth middleware when JWT is wired up.
- Use `HttpContextExtensions.CurrentUserId()` in controllers.

---

## Configuration

Local dev: copy `.env.example` → `.env`.

| Variable | Purpose |
|----------|---------|
| `DB_CONNECTION_STRING` | MySQL connection string |
| `LISTENING_URL` | Bind URL (default `http://0.0.0.0:5000`) |
| `JWT_ISSUER_URL` | OIDC issuer (empty = auth disabled) |
| `JWT_APP_IDENTIFIER` | API audience |

`.env` is gitignored. Do not commit secrets.

---

## What NOT to Add

- Entity/model files with static Dapper helpers (SQL belongs in handlers).
- EF Core or generic repositories.
- Minimal APIs for business endpoints.
- Global `Result<T>` wrappers — each feature defines its own `Result`.
- New NuGet packages without a clear need.
- `Services/` or background jobs until the feature actually requires them.

---

## Checklist: New Endpoint

- [ ] SQL migration added (if schema changes) — next numbered file in `Database/Scripts/`
- [ ] Feature file in `Features/{Domain}/{UseCase}.cs` with handler + Result
- [ ] Validator added (commands only)
- [ ] Controller action binds to Query/Command and calls `_mediator.Send`
- [ ] No SQL in controller; no HTTP types in handler
- [ ] `dotnet build` passes

---

## Running Locally

```bash
cd DammaniAPI
cp .env.example .env   # edit DB_CONNECTION_STRING
dotnet run
```

- Health: `GET /health`
- Migrations run on startup (warning logged if MySQL is unavailable — app still starts)
