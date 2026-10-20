# Gamefilled V2 — account security migration (design gate)

Status: **design only**. No database migration, mail provider, route or authentication
behaviour is enabled by this document. Do not publicly expose `gamefilled.pt`
while the gates below remain open.

## Existing system and threats

- The existing `Users` table has integer `Id` keys referenced by follows,
  libraries, reviews and notifications. Preserve them and every FK.
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

1. Integrate **ASP.NET Core Identity** using the current `Users` IDs and
   existing profile/social/library rows. Evaluate a custom `IdentityUser<int>`
   or compatible store carefully; do not drop/recreate `Users` or alter existing
   IDs. Add required Identity fields/tables using reviewed EF migrations.
2. On initial login, a custom `IPasswordHasher` may verify existing BCrypt
   hashes and return `SuccessRehashNeeded` to migrate on successful login to
   a supported Identity hash. Never force password resets without a recovery
   route. Verify malformed hashes are handled safely.
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
- After registering, do not permit unverified new accounts to log in or perform
  authenticated actions. To avoid locking out legacy accounts abruptly, apply
  an explicit documented migration policy and invite existing users to verify.
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
   migration and backward compatibility; inspect `/Settings/Account` and
   all POST handlers. Confirm DNS manager and outbound mail domain.
3. **Build:** implement on isolated feature branch with a reviewed EF Core
   migration and dedicated integration tests. Existing usernames, BCrypt
   passwords, FKs, libraries and profile fields must survive. CI must be green.
4. **Security tests:** verify invalid/expired/used tokens, account enumeration,
   reset session invalidation, HTTPS-origin enforcement, CSRF, rate limits,
   duplicate emails, password/role changes, unverified access, and legacy
   account compatibility. Include database-backed integration tests.
5. **Staging:** run the migration on a *restored clone* with a test mail domain
   before applying to the actual Gamefilled SQL Server. Validate app startup,
   mail delivery, rollback/restore path and browser sessions.
6. **Production:** deploy from an exact tested commit only after the mail domain
   is verified and the checklist is signed off; monitor errors without logging
   secrets. Publish external access only after broader security audit.

## Non-goals of this first stage

- No public DNS/router/firewall opening.
- No immediate mandatory re-verification of legacy accounts.
- No automatic in-place change of existing BCrypt hashes.
- No custom password reset table/token scheme while Identity is being adopted.
- No claim that a CI pass alone certifies the system as breach-free.
