# Gamefilled V2 Identity foundation — isolated prototype

> This directory is an **unpublished foundation**, not a replacement for V2
> and not yet a functional account UI. There is no registration/login or email
> recovery endpoint, and no generated modernized V2 SQL migration committed yet.

## What is implemented

- Separate ASP.NET Core 10 application at `v2-modernization/Gamefilled.modernized V2`.
- Dedicated `GamefilledIdentityDbContext` based on Identity EF stores using
  GUID account IDs; no dependency on V2 custom BCrypt/session authorization.
- Unique normalized email index; confirmed email required to sign in.
- Password minimum 12 characters, secure, HttpOnly, SameSite=Lax auth cookies,
  security stamp checks approximately each minute and no sliding expiration.
- Trusted-forwarded-header support configured only for explicitly listed
  reverse-proxy network.
- Only `GET /health/live` (anonymous) and `GET /identity/me` (authenticated)
  currently exposed. **Do not use this as a public account service yet.**
- Tests compile and run as a second project in the existing GitHub CI.

## Database isolation

- Existing V2 uses `ConnectionStrings:DefaultConnection`, database
  `Gamefilleddb` on the Docker SQL Server instance.
- modernized V2 reads **only** `ConnectionStrings:modernized V2Database`. Use a different,
  initially empty database (`Gamefilledmodernized V2`) with separate credentials and
  a least-privileged runtime SQL user. The application does not create or
  migrate a database on startup.
- No V2 data should be deleted, even though existing accounts are disposable.
- Before making schema changes or launching a test container: verify the SQL
  backup is copied outside the VM and has been restored successfully.
- Add the first EF Core migration on the modernized V2 branch *before any deployment*.
  Its generated SQL must be reviewed to ensure **zero DROP/ALTER of V2
  tables/databases** and must be tested against a blank modernized V2 SQL database.

Example EF migration generation using the repository-pinned dotnet-ef tool
(run in a checked-out branch using .NET 10 SDK):

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add InitialIdentity \
  --project v2-modernization/Gamefilled.modernized V2/Gamefilled.modernized V2.csproj \
  --startup-project v2-modernization/Gamefilled.modernized V2/Gamefilled.modernized V2.csproj \
  --output-dir Data/Migrations
```

For the design-time startup, supply the new *modernized V2* connection string through
environment variables or .NET User Secrets. Never print real credentials
in workflow logs or commit them. Do not run `database update` until the
migration is reviewed and an isolated database is ready.

## Pending before any deployment

- Password reset and confirmation emails through the existing tested Resend
  transport, using an external mounted secret (not a committed API key).
- Functional Identity registration, login, logout and verification pages,
  with generic account-reset responses and rate limiting.
- Database-backed tests for registration, CSRF, email confirmation, expired
  links, password change, security-stamp revocation and duplicate email races.
- Database-backed library, social, notifications and moderation parity.
- Secure persistent ASP.NET Data Protection keys, a real HTTPS-only app URL,
  least-privileged DB permissions, validated infrastructure and observability.
- Verified restore and release checklist before any real or public cutover.

## No impact on live V2

The existing `/opt/gamefilled` Compose still builds from `./src`.
**Do not run `docker compose down -v`**, change its SQL connection string,
or deploy the modernized V2 host just because this branch passes CI. CI proves a
foundation compiles, not that the security migration is complete.
