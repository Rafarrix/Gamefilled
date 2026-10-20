# Gamefilled V2 — account security migration (design gate)

Status: **design only**. No database migration, mail provider, route or authentication
behaviour is enabled by this document. Do not publicly expose `gamefilled.pt`
while the gates below remain open.

## Decision update — 2026-10-20: test accounts and data are disposable

The project owner explicitly confirmed that current Gamefilled accounts and library
entries are test data and may be discarded. Prefer a **clean-slate ASP.NET Core
Identity implementation on a NEW, isolated SQL Server database**, rather than a
complex backwards-compatible migration of old sessions and BCrypt accounts.

- Treat the existing Gamefilled database as a **source to preserve temporarily**,
  not as a database to modify during development. Back it up off-host and test a
  restore, even if its contents are disposable, so infrastructure rollback works.
- Create and migrate the new database independently using reviewed EF migrations.
  Keep the Gamefilled game catalog/IGDB/social/library features, but update all
  application references to use a single, canonical Identity account model and
  authenticated ClaimsPrincipal. No custom session-based fallback.
- Existing test accounts **do not need migration**. After a supervised cutover,
  users register again, confirm their email and repopulate their collections.
- Do not delete, replace or clear the current SQL data until the clean Identity
  deployment has passed integration tests, the user has confirmed cutover and
  the old backup is safely stored.
- Passwords must use the standard framework hasher. Implement the official
  Identity login, email verification, password reset, security-stamp validation
  and account settings flows. Avoid hand-rolled security token storage.
- Evaluate moving auth data into the same SQL database as the social/library
  entities (recommended for referential integrity) with explicit mappings,
  constraints and migration tests.
- Minimize the transition period when both authentication mechanisms could be
  active: the new version must not authorize requests based on legacy session keys.

## Existing system and threats

- The existing `Users` table has integer `Id` keys referenced by follows,
  libraries, reviews and notifications. Existing data is disposable, so the
  **new database** may use a fresh Identity user schema and new IDs, provided all
  the new social/library foreign keys are correct and verified.
- Passwords use BCrypt hashes. Current login writes `userId`, `username`,
  `displayName` and `role` to ASP.NET Core Session. Authorization across
  Razor Pages often reads those session values rather than
  `HttpContext.User`; replacing a single login route is insufficient.
- `/Settings/Account` currently changes the email directly after current-password
  validation, without verifying control of the replacement inbox. This MUST be
  redesigned before enabling verified-email features.
- Application runs behind Caddy with internal TLS for `dev.gamefilled.pt`.
  Session cookie/Data Protection keys need to survive container restarts;
  check existing bind mounts and cookie configuration before cutover.
- Existing accounts cannot be silently marked as verified: possession of an
  email address in the database is not proof of inbox control.
- Login attempt protection currently includes an in-memory per-identifier lock,
  which is susceptible to deliberate temporary account lockouts; consider safer
  per-IP/account backoff and monitoring before internet launch.

## Target authentication

1. Integrate **ASP.NET Core Identity** on a separate database using its
   supported Entity Framework Core stores and a custom application user for
   profile fields. Design the new FK model and reviewed EF migrations without
   preserving throwaway account IDs or existing BCrypt hashes.
2. Remove all custom password verification/authentication and migrate to the
   supported Identity password hasher and account management APIs; legacy users
   re-register after the explicit cutover.
3. Replace all session-based authentication/authorization consumers with a
   supported Identity principal + authorization policies. Cover every mutating
   Razor Page/handler, follow, library, notification and settings operation.
   Refuse an incomplete hybrid where old session keys can bypass Identity.
4. Configure authentication cookies `Secure`, `HttpOnly`, suitable SameSite,
   finite lifetime and an explicit login/logout boundary. Use antiforgery on
   state-changing actions. Verify no session-fixation regression.
5. Use Identity security stamps; password reset/change, role change and account
   recovery must invalidate existing authentication tickets promptly.
   Configure and test validation intervals; confirm logout everywhere behavior.
6. Preserve a tested rollback image and database backup; old code may not
   understand schema/data updated by new code, so do not assume image rollback
   alone is sufficient.

## Email verification and recovery

- Use Identity token providers (Data Protection) rather than custom reusable
  secrets or self-built cryptography. Configure purpose-scoped expiry
  (suggested: email verification 24 h; password reset 30 minutes) and
  single-use behaviour through Identity's security stamp changes.
- After registering, do not permit unverified accounts to log in or perform
  authenticated actions. Existing test accounts will not be migrated or
  silently marked as verified; users will re-register.
- Requesting a reset or verification email must use the same generic response
  regardless of whether the email is registered, avoiding account enumeration.
  Apply anti-abuse throttling to send/resend routes and review email cost
  controls. Do not put tokens, provider API keys or password content in logs.
- Reset links must use a **configured, allowlisted HTTPS public origin**,
  not arbitrary `Host` / forwarded-host header values. URL-safe tokens only;
  no tokens in third-party analytics or Referer.
- Confirm email ownership *before* granting verified status. Email changes must
  use a pending email and confirmation sent to the new address; changing
  `Users.Email` and its verification state immediately is not acceptable.
  Require recent password reauthentication for sensitive settings changes.
- In production: if mail provider/configuration is unavailable, **fail closed**.
  No preview/debug link containing a usable token may be shown to a visitor.
  Development/test email sinks must be impossible to enable in Production.
- Senders: prefer a dedicated restricted transactional-email API key, held in
  host-side secrets, not Git, tests, logs, `docker compose config` output or
  chat. Send only from a domain verified by the provider.
- Provider candidate: Resend. Set up domain verification in the existing DNS
  zone only after taking an inventory of MX, SPF, DKIM, DMARC and old A/CNAME
  records. Add records exactly as shown by provider, without clobbering existing
  SPF/MX, and do not change public web A/AAAA/CNAME records or router ports.
  Confirm sender authenticity, actual message receipt and spam delivery.

## Staging and release gates

1. **Backups:** copy encrypted database backup and required files *off the VM*.
   Test a full database restore to an isolated instance before any migration.
   Protect the provider key and Data Protection key material separately.
2. **Design:** inspect all authorization consumers, agree on Identity storage,
   clean database cutover; inspect `/Settings/Account` and
   all POST handlers. Confirm DNS manager and outbound mail domain.
3. **Build:** implement on isolated feature branch with a reviewed EF Core
   migration and dedicated integration tests. Old accounts and their test
   library rows need not survive; all **new** identity/social/library FKs,
   account workflows and authorizations must pass. CI must be green.
4. **Security tests:** verify invalid/expired/used tokens, account enumeration,
   reset session invalidation, HTTPS-origin enforcement, CSRF, rate limits,
   duplicate emails, password/role changes, unverified access, and rejection
   of legacy session-only authentication. Include database-backed integration tests.
5. **Staging:** run EF migrations on a brand-new **isolated** database and
   use a test sending domain. Validate startup, email delivery, login, stored
   collections and denial of access using the old session cookie. Keep the
   original database unchanged for recovery.
6. **Production:** deploy from an exact tested commit only after the mail domain
   is verified and the checklist is signed off; monitor errors without logging
   secrets. Publish external access only after broader security audit.

## Non-goals of this first stage

- No public DNS/router/firewall opening.
- No migration/re-verification of existing throwaway accounts; users register
  again after an explicit cutover.
- No automatic in-place change of existing BCrypt hashes.
- No custom password reset table/token scheme while Identity is being adopted.
- No claim that a CI pass alone certifies the system as breach-free.

## Mail provider / data-protection assessment

- Resend's Ireland (eu-west-1) setting is an **email sending region**, not EU
  data residency. As of October 2026, Resend says message content and logs
  are stored in the United States. Review the DPA and update the Gamefilled
  privacy notice before publication. Prefer minimal account email content
  and short retention where configurable.
- Domain DNS verification alone never requires publishing a web server or
  opening router ports. Review provider DKIM, SPF and bounce/return-path MX
  records for conflicts before editing DNS. Never replace an existing primary
  domain MX without deliberate review.
