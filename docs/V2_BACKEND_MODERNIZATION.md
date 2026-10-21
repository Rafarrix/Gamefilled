# Gamefilled V2 — backend modernization plan

Status: **design and inventory for post-PAP V2**. The existing currently hosted V2 containers remain running and no SQL data is removed. Existing accounts and saved games are disposable test data by the owner's decision. The existing front-end is a visual baseline, not an immutable requirement.

## Decision

Rebuild the authentication and weak backend pieces professionally rather than maintaining compatibility with custom BCrypt/session authorization. Use a **modular monolith**, not microservices: ASP.NET Core 10 Razor Pages, ASP.NET Core Identity with EF stores, SQL Server and reviewable EF Core migrations, typed HttpClients for IGDB/Resend, Caddy for HTTPS. Preserve the well-working visual system, improve secondary pages, and eliminate abandoned placeholders or implement them correctly.

Create a NEW isolated database for the modernized V2 backend. Do not change the V2 database or deploy the modernized V2 publicly until acceptance gates pass. After cutover, discard old disposable accounts only with the owner's explicit go-ahead and a restorable off-host backup.

## Verified repository inventory (2026-10-20)

| Feature | Current implementation | the modernized V2 decision |
| --- | --- | --- |
| Register/Login | Custom BCrypt, session IDs/role, rate-limit memory cache | Replace with Identity; email verification required |
| Settings/Account | Password/email changes against session user ID | Replace with Identity; email change requires new-address verification and recent auth |
| Games catalog and detail | IGDB clients, Razor Pages and API-style handlers | Retain experience; centralize typed clients, caching, timeouts and limits |
| Per-user library and reviews | UserGameEntries and direct DB handlers | Keep feature; link to Identity user FK and authorize by claims/current user |
| Follows/friends/activity/notifications | EF-backed social services, some session checks | Keep feature; rewrite security boundaries, ownership checks and POST semantics |
| Settings/Blocked, Infractions, Integrations, Defaults | Current GET handler only checks Session userId; page implementations need product review | Mark placeholder/unverified; hide or implement real policy/data-backed workflows |
| Public Contact | Explicit prelaunch placeholder | Implement support/abuse/contact workflow before launch |
| Terms & Privacy | Pre-release drafts | Replace with reviewed public policy, GDPR rights, moderation terms |
| IGDB search API | Legacy handler uses IgnoreAntiforgeryToken for GET, manual outbound HTTP and detailed configuration-error message | Consolidate, validate query lengths, cap calls, safe error responses, IGDB quota and caches |
| Mail | Resend sender transport committed but not wired to account flows | Use fixed sender, secrets mounted, token flow via Identity |

## Architecture: modules, not independent services

- **Identity/Accounts**: single Identity authority, claims-based authorization only; no legacy session security fallback; verified email + login + logout + reset/change password + revoke sessions; CSRF, cookie security, safe redirects, antiforgery and reauthentication.
- **Catalog**: typed IGDB integration, bounded queries, controlled retries/backoff, caching, no tokens in client JS, safe images and upstream error handling.
- **Library**: status, reviews, ratings and favorites; enforce ownership in server-side queries; optimistic concurrency and bounded input.
- **Community**: follows/friends, activity and notification delivery with POST-only state changes, antiforgery, ownership policies, privacy settings and spam limits.
- **Settings**: personal profile, account, notifications, privacy and blocked users; only surface features that actually work.
- **Moderation**: reporting, user blocks, limits, audit records, admin authorization separate from ordinary users, appeal/review workflow before public social release.
- **Operations**: health, structured sanitized logs, background work when needed, audit/monitoring, tested backups/restore, rollback and secret rotation.

Use explicit service interfaces for each module. Keep Razor Page models thin and prevent duplicated SQL/business rules in views and APIs. Do not expose the DB or backend containers publicly.

## the modernized V2 delivery milestones

1. **Design and risk inventory** — routes, all POST/GET handlers, session reads, EF queries, JS innerHTML, external URLs and existing UI reviewed; identify required vs optional features.
2. **Database and Identity baseline** — create isolated the modernized V2 SQL database and EF migrations; add Identity entities with appropriate indexes, ownership FKs and delete rules. Add database-backed integration tests. Never use `EnsureCreated` in deployment.
3. **Full authorization cutover** — move all current-user lookups to Identity/ClaimsPrincipal; no `Session.GetInt32("userId")` as an authority. Validate unauthorized access across every mutation. Test login, logout, CSRF, password reset and security-stamp session invalidation.
4. **Mail flows** — verification, re-send and recovery through already-implemented Resend transport. Tokens are handled by Identity; fixed HTTPS origin; generic replies; no tracking or secret logging. Secret-file access validated with non-root container.
5. **Feature parity** — catalog, game details, libraries, ratings/reviews, user profiles, follows/friends and notifications must pass functional and security integration tests. Reuse CSS and frontend components but improve rough pages.
6. **Secondary pages + moderation** — evaluate every placeholder: implement completely or hide until viable; privacy, blocks, reports, admin tools, support, policy pages.
7. **Staging and release** — production-like staging database with tested migrations, off-host backup/restore, CI security scanning, code review and abuse tests; switch production image/database only after user acceptance. Keep V2 untouched until then.

## Release quality gates

- No high/critical known exploitable auth or authorization defects, no account enumeration in reset flows, no raw credentials or reset links in logs, rate limits at edge and app.
- All state changes use POST/PUT/etc with CSRF protection; input and output are validated/encoded, no XSS via user-controlled URLs or dynamic HTML.
- Identity email must be verified before access; invalid/expired/used tokens rejected; stolen session invalidated after password reset/recovery.
- DB app connection uses a least-privilege login, migration account separated.
- SQL restore tested from an off-host copy; key material also backed up appropriately.
- Domain publication requires explicit infrastructure review of DNS, Caddy TLS, router forwarding and Proxmox isolation. No automatic public exposure.
- All gates are evidence-based: CI tests and manual acceptance, not a claim of invulnerability.

## Near-term implementation order

Start with the modernized V2 **new Identity schema and database**, not polishing the legacy login. In parallel, collect a route/permission matrix and a secondary-page inventory. Only add one authenticated module at a time. Preserve the existing web UI until a feature is verified, then switch its backend.
