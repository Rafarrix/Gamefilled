# Unified ASP.NET Core Identity domain in the original Gamefilled

**Status:** first real code integration slice, 2026-10-22. The
**original Gamefilled.csproj** now compiles the future consolidated
ASP.NET Core Identity domain, not another website.

## Integrated source / proposed future database

- `Identity/GamefilledAccount.cs` contains one `IdentityUser<Guid>`
  (email, profile, avatar, bio and visibility).
- `Identity/GamefilledDomain.cs` contains the account-owned game entries,
  featured favorites, follow graph, activity and notification entities,
  all with GUID user ownership, not the old integer session user ID.
- `Data/GamefilledV2DbContext.cs` is one
  `IdentityDbContext<GamefilledAccount, IdentityRole<Guid>, Guid>`.
  Normalized emails have a unique database index. Game/library status,
  rating, reviews and follow pairs have database-level constraints,
  uniqueness, GUID foreign keys and ownership links.
- `tests/Gamefilled.Tests/UnifiedIdentityDomainTests.cs` checks the
  canonical model's EF metadata, uniqueness and foreign key types.
- `Data/GamefilledV2DbContextFactory.cs` allows *design-time* migration
  generation without defaulting to the real development DB.
- The existing legacy EF baseline/migration bundle explicitly uses
  `AppDbContext` so merely introducing this new context cannot
  unexpectedly migrate the running SQL database.

**This is schema/model source, NOT yet the active authentication system.**
The current `GamefilledFullPreview` runtime still uses legacy sessions,
and no GUID schema migration has been generated/applied or provisioned on
`docker-01`. Do not claim new auth security is active in the browser yet.

## Required next implementation gates

1. Generate and code-review an `InitialUnifiedIdentity` EF migration
   **for `GamefilledV2DbContext` only** and apply it to a new, separate
   disposable SQL database `GamefilledV2Integrated` on the preview VM.
   Never migrate or overwrite `web-01` / `Gamefilleddb`.
2. Use one DbContext and the new principal for every authenticated page
   and handler. Adapt `/users/Login`, `/users/Register` and the existing
   visual form templates, integrate tested Resend verification, reset and
   account settings, and ensure Identity cookies/security stamps.
3. Replace every `Session.GetInt32/GetString("userId"/"username")` authority
   in profiles, library, reviews, follows, people, favorites, activity,
   notifications and settings. No compatibility login or dual-authority
   fallback in a live environment.
4. Test email verification, password reset, invalid/reused tokens, cookie
   revocation, CSRF, per-user ownership, concurrency, unique indexes,
   FKs, status constraints, and end-to-end browser parity.
5. Only switch the preview to the integrated SQL-backed app once all
   handlers pass; keep main layout, logo/backgrounds, UI, card CSS and
   IGDB query strategies unchanged.
6. Archive/remove legacy auth models/services/tables/code only *after*
   clean-suite tests show no live runtime dependencies. Never delete the
   preview data or live database to paper over an incomplete migration.
7. Off-host encrypted backup/restore rehearsal and explicit owner's
   acceptance are compulsory before deploying to the real site.

See `docs/V2_AUTH_CUTOVER_MATRIX.md` and GitHub issue #46.
