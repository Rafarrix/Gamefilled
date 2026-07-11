# Gamefilled production foundation

This document describes the first infrastructure baseline. It deliberately does not migrate authentication or modify the database schema.

## Requirements

- .NET 10 SDK. `global.json` allows supported .NET 10 feature-band roll-forward.
- SQL Server reachable by the application.
- Twitch/IGDB client credentials.
- Docker only when building or running the production image.

## Local configuration

Keep real credentials outside Git.

Choose one approach:

1. Copy `appsettings.example.json` to an ignored `appsettings.Development.json` and replace the placeholders.
2. Use ASP.NET Core user secrets.
3. Set environment variables:

```text
ConnectionStrings__DefaultConnection
IGDB__ClientId
IGDB__ClientSecret
```

The application now fails at startup with a clear message when required configuration is absent.

## Local build

```bash
dotnet --info
dotnet restore
dotnet build
dotnet run
```

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

1. NuGet restore;
2. Release build;
3. Release publish;
4. production Docker image build.

Tests and security scans are added in subsequent slices of issue #10.

## Deferred intentionally

- ASP.NET Core Identity migration;
- EF Core migration baseline;
- persistent Data Protection keys;
- Docker Compose database bootstrap;
- deployment workflow;
- production secrets integration;
- CSP and stricter browser isolation headers;
- automated tests and security scanning.

These are kept separate so failures can be diagnosed and rolled back without mixing authentication, schema and runtime changes.
