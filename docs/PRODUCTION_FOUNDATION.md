# Gamefilled production foundation

This document describes the first infrastructure baseline. It deliberately does not migrate authentication or modify the database schema.

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

1. NuGet restore for the solution;
2. Release build for application and tests;
3. automated smoke tests;
4. Release publish for the web application;
5. production Docker image build.

The build and test logs are uploaded as workflow artifacts for diagnosis.

## Deferred intentionally

- database-backed integration tests;
- ASP.NET Core Identity migration;
- EF Core migration baseline;
- persistent Data Protection keys;
- Docker Compose database bootstrap;
- deployment workflow;
- production secrets integration;
- CSP and stricter browser isolation headers;
- dependency, CodeQL and secret scanning.

These are kept separate so failures can be diagnosed and rolled back without mixing authentication, schema and runtime changes.
