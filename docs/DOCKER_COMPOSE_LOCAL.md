# Gamefilled local Docker Compose

This stack is for local development and isolated testing. It does not replace an existing `SQLEXPRESS` database and it does not apply migrations from the web application's startup path.

## Services

- `sqlserver`: SQL Server 2025 Developer on container port `1433` and host port `14333` by default.
- `migrations`: a one-shot EF Core migration bundle. It waits for SQL Server, applies only missing migrations, and exits.
- `app`: the normal non-root Gamefilled runtime image. It starts only after the migration service succeeds.

The SQL data is stored in the named volume `gamefilled-sql-data`.

## Requirements

- Docker Desktop with Linux containers enabled.
- Docker Compose v2.
- At least 2 GB of memory available to SQL Server, with additional memory for the app and Docker Desktop.
- Valid Twitch/IGDB credentials.

## First start

From the repository root:

```powershell
Copy-Item .env.example .env
```

Edit `.env` and replace:

```text
MSSQL_SA_PASSWORD
IGDB__ClientId
IGDB__ClientSecret
```

Validate the resolved Compose configuration without printing secrets into chat or documentation:

```powershell
docker compose config --quiet
```

Build and start the complete stack:

```powershell
docker compose up --build
```

Open:

```text
http://localhost:5000
http://localhost:5000/health/live
http://localhost:5000/health/ready
```

## Background mode

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f app
docker compose logs -f migrations
docker compose logs -f sqlserver
```

## Connect from SSMS

Use SQL Server Authentication:

```text
Server: localhost,14333
Login: sa
Password: the value from MSSQL_SA_PASSWORD in .env
Trust server certificate: enabled
```

The container database is independent from `localhost\SQLEXPRESS`.

## Migration behavior

The `migrations` service uses the EF migration bundle built from `Data/Migrations`.

On a new empty volume it creates `Gamefilleddb` and applies the current migrations. On later starts it checks `__EFMigrationsHistory`, applies only missing migrations, and exits successfully when the database is already current.

The app itself does not call `Database.Migrate()` during startup.

Run the migration service explicitly:

```powershell
docker compose run --rm migrations
```

## Stop and restart

Stop containers while preserving SQL data:

```powershell
docker compose down
```

Start again with the same data:

```powershell
docker compose up -d
```

## Reset the container database

This permanently deletes only the Docker volume database. It does not touch `SQLEXPRESS`.

```powershell
docker compose down -v
docker compose up --build
```

## Change ports

Edit `.env`:

```text
GAMEFILLED_HTTP_PORT=5001
MSSQL_HOST_PORT=14334
```

The internal service addresses remain `app:8080` and `sqlserver:1433`.

## Troubleshooting

Check service state:

```powershell
docker compose ps
```

Check SQL Server startup:

```powershell
docker compose logs sqlserver
```

Check migrations:

```powershell
docker compose logs migrations
```

A SQL Server container that exits immediately usually indicates that `MSSQL_SA_PASSWORD` does not satisfy SQL Server password complexity or that Docker has insufficient memory.

Rebuild without cache only when diagnosing an image-build problem:

```powershell
docker compose build --no-cache app migrations
```

## Safety boundaries

- `.env` is ignored by Git and must never be committed.
- The example credentials are placeholders only.
- The `sa` login is acceptable for this isolated local stack, not for the future production deployment.
- Production migrations will remain an explicit reviewed deployment action rather than an automatic web-app startup behavior.
