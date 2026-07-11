# Gamefilled production foundation

This document describes the first infrastructure baseline. It preserves existing product behavior and never modifies an existing database automatically.

## Requirements

- .NET 10 SDK. `global.json` allows supported .NET 10 feature-band roll-forward.
- Visual Studio 2026 18.0+ when using Visual Studio.
- SQL Server reachable by the application.
- Twitch/IGDB client credentials.
- Docker only when building or running the production image.

## Local configuration

Keep real credentials outside Git.

Preferred for local development: ASP.NET Core User Secrets.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"
dotnet user-secrets set "IGDB:ClientId" "<client-id>"
dotnet user-secrets set "IGDB:ClientSecret" "<client-secret>"
```

Equivalent environment variable names are:

```text
ConnectionStrings__DefaultConnection
IGDB__ClientId
IGDB__ClientSecret
```

`appsettings.example.json` and `.env.example` contain placeholders only. Real values must never be committed.

The application fails at startup with a clear message when required configuration is absent.

## Local build and tests

```bash
dotnet --info
dotnet tool restore
dotnet restore Gamefilled.sln
dotnet build Gamefilled.sln
dotnet test tests/Gamefilled.Tests/Gamefilled.Tests.csproj
dotnet run --project Gamefilled.csproj
```

The test project uses isolated test-only configuration and does not read the developer's User Secrets.

Current smoke-test contracts:

- the liveness endpoint returns the expected service contract;
- baseline security headers are present;
- an anonymous visitor cannot open the notification inbox.

These tests are intentionally small. They establish the pipeline before database-backed login, follow, library and notification tests are added.

## EF Core baseline

The canonical initial migration is stored in `Data/Migrations` and represents the existing Gamefilled schema through Notifications V1.

For a brand-new empty database, migrations can eventually create the schema normally.

For an existing database that already contains Gamefilled tables and data, never run `dotnet ef database update` until the database has been audited and adopted into migration history.

Use this sequence:

1. Back up the database.
2. Run `Database/schema-audit.sql` in SSMS.
3. When the audit reports known differences, run `Database/upgrades/2026-07-11-reconcile-ef-baseline-schema.sql` rather than editing data manually.
4. Run `Database/upgrades/2026-07-11-ef-baseline-support-indexes.sql`.
5. Run `Database/schema-audit.sql` again and require `PASS`.
6. Run `Database/upgrades/2026-07-11-adopt-ef-initial-baseline.sql`.
7. Confirm that it reports migration `20260711171753_InitialBaseline` and product version `10.0.9`.
8. Run `dotnet ef database update`; it must report that the database is already up to date.
9. From then on, future schema changes use normal reviewed EF Core migrations.

The reconciliation script converts the legacy `Users.CreatedAt` column to `datetime2` and creates required unique indexes. It first detects duplicate usernames, follow pairs and favorite-game positions. When duplicates exist, it stops before any schema change and reports the conflicting keys; it never deletes or merges rows automatically.

The adoption script is idempotent. It validates the essential tables, indexes and check constraints before creating `dbo.__EFMigrationsHistory` and inserting only the canonical baseline row. It does not recreate or alter application tables.

The CI runs `dotnet ef migrations has-pending-model-changes`. A model change without a corresponding migration therefore fails the pipeline.

## Health endpoints

- `GET /health/live` confirms that the process and HTTP pipeline are running.
- `GET /health/ready` confirms that the application can reach SQL Server.

Orchestrators should use `live` for liveness and `ready` for readiness.

## Production image

Build:

```bash
docker build -t gamefilled:local .
```

Run with environment variables supplied by the host or secret store:

```bash
docker run --rm -p 5000:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="<connection-string>" \
  -e IGDB__ClientId="<client-id>" \
  -e IGDB__ClientSecret="<client-secret>" \
  gamefilled:local
```

The final image runs as the non-root `app` user and listens on port `8080`.

## CI guarantees in this slice

Every feature branch and pull request must complete:

1. local .NET tool restore;
2. NuGet restore for the solution;
3. Release build for application and tests;
4. automated smoke tests;
5. EF migration snapshot consistency check;
6. Release publish for the web application;
7. production Docker image build.

The build and test logs are uploaded as workflow artifacts for diagnosis.

## Deferred intentionally

- database-backed integration tests;
- ASP.NET Core Identity migration;
- persistent Data Protection keys;
- Docker Compose database bootstrap;
- deployment workflow;
- production secrets integration;
- CSP and stricter browser isolation headers;
- dependency, CodeQL and secret scanning.

These are kept separate so failures can be diagnosed and rolled back without mixing authentication, schema and runtime changes.
