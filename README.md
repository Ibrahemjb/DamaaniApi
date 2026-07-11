# DammaniAPI

.NET 8 API for Damaani (digital warranty cards).

## Local admin demo

1. Copy `.env.example` to `.env` and set `DB_CONNECTION_STRING`.
2. Keep these flags for a ready-made admin console:

```
PLATFORM_ADMIN_EMAIL=admin@damaani.local
SEED_DEMO_DATA=true
```

3. Run `dotnet run`. On first start with seed enabled, the API creates:

| Account | Password | Role |
|---------|----------|------|
| `admin@damaani.local` | `Admin123!` | Platform admin (super) |
| `owner1@demo.damaani.local` … | `Owner123!` | Demo shop owners |

Seed is idempotent (skips if the demo admin already exists). Turn `SEED_DEMO_DATA` off in shared/staging environments.

4. Open the web app, log in as the admin, and land on `/admin`.
