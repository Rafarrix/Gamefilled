# Gamefilled V2 — modernized Identity foundation

Status: **private, isolated foundation** for the evolving Gamefilled V2.
It does not yet replace the **currently deployed revision of V2** on web-01.

## Implemented

- Separate ASP.NET Core 10 application in
  `v2-modernization/Gamefilled.V2`, kept outside the current V2 publish.
- ASP.NET Core Identity using EF Core SQL Server, GUID account IDs and
  a unique normalized email index.
- Required confirmed email for sign-in, minimum 12-character password,
  cookie-only authentication and prompt security-stamp validation.
- Secure, HttpOnly, SameSite=Lax authentication cookie and explicit trusted
  reverse-proxy IP ranges.
- Initial `/health/live` and authenticated `/identity/me` endpoints.
  These are **not** a functional registration/login user interface yet.
- Separate test project `tests/Gamefilled.V2.Tests` in GitHub CI.

## Strict SQL separation

- Currently hosted V2: `ConnectionStrings:DefaultConnection`, database
  `Gamefilleddb`. Do not touch this database from the new host.
- Modernizing V2 host: `ConnectionStrings:V2ModernDatabase`, to be
  connected only to a **new database**, for example `GamefilledV2Modern`.
- Separate database credentials and least privileges for normal runtime.
  Review, generate and test EF Core migrations on an isolated database;
  **no automatic database migration on application startup**.
- Confirm off-host backup and tested restore before using production SQL.
  Do not delete existing accounts without explicit cutover confirmation.

To generate future migrations safely with the repository's pinned EF
tool in a checked-out branch (the SDK and a test-only connection string
must be configured):

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add NameOfNextSchemaChange \
  --project v2-modernization/Gamefilled.V2/Gamefilled.V2.csproj \
  --startup-project v2-modernization/Gamefilled.V2/Gamefilled.V2.csproj \
  --context GamefilledIdentityDbContext \
  --output-dir Data/Migrations
```

This command **generates migration source files**, but does not apply them.
Review them for unintended operations before using `database update`.

## Not implemented yet

- The **InitialIdentity migration has now been generated and committed**:
  `20261021192111_InitialIdentity`. It creates only seven `AspNet*`
  Identity tables and seven indexes in a new database; it has never been
  applied to the existing Gamefilleddb.
- A staged SQL database and database-backed integration tests are still
  pending.
- Real registration/login/logout, verified-email and password reset flows.
- Resend transport hookup using a secret mounted outside source/image.
- Complete games, library, review, profile, social, notification and
  moderation feature parity.
- Data Protection key persistence for the modernized host and full security
  review of endpoint authorization.
- Live production cutover and any public DNS/router exposure.

A passing build or unit test is not proof of readiness for public access.
