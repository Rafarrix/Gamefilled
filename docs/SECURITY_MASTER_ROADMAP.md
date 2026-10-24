# Gamefilled — Security-first V2 roadmap and final code overhaul

Last reviewed: 2026-10-24. **Canonical website: `Gamefilled.csproj`.**
Keep `Pages/**`, `wwwroot/**`, game search/IGDB behavior and visual identity
unchanged except for specifically reviewed fixes. The previous isolated
`v2-modernization/Gamefilled.V2` is a **source of proven components**;
it is **not a replacement website**.

## Unbreakable environment contract

- `docker-01` private HTTPS `preview.gamefilled.pt:8444` on preview-only SQL:
  all development, staging, browser, integration and attack-simulation tests.
- `web-01` / existing development and public service stays untouched until
  the owner explicitly accepts a complete tested V2 and an off-host restored backup.
- No public ingress for the private preview, no changing router rules or DNS.
- Never run `docker compose down -v` or apply a migration to live `Gamefilleddb`.
- No parallel session-and-Identity account authority, nor legacy login fallback.
- Never silently weaken or recalibrate IGDB search, categories, special queries,
  or results. Changes need before/after manual relevance review.
- No secret files in Git/CI logs, container images, screenshots or messages.

## Security milestones — finish before code overhaul

| Gate | Deliverable | Required evidence |
| --- | --- | --- |
| S0. Baseline | Live UI/routes inventory, identity/session map, threat model, rollback boundaries | Route matrix, screenshot fixtures desktop/mobile, automated legacy-session growth guard |
| S1. Infrastructure least privilege | Dedicated web SQL login in preview; admin credentials only in setup; separate network/volumes; non-root web | CI SQL account cannot CREATE TABLE, ALTER, db_owner or sysadmin; isolated port; preview smoke tests |
| S2. Unified data | Single GUID Identity account, reviews, library, follows, blocks, activities, notifications, favorites | EF migrations on disposable SQL, FK/unique/check constraints, idempotent migrations and backups |
| S3. Account security | Original branded login/register + Identity, verified email, unique username/email, password reset, resend, security stamps, lockout and antiforgery | Browser E2E on isolated accounts; reset tokens single-use; generic account recovery; session revocation |
| S4. **Atomic removal of legacy authorization** | All PageModels/APIs/services use one principal; replace `Session.Get*(userId/username)`, BCrypt handlers and direct `int` ownership | CI rejects ALL legacy session account authority; anonymous/forged/cross-account POST tests; no dual auth; same original UI |
| S5. Profile/community privacy | Block/follow/visibility/notification settings; authorized mutations and validated redirects | Denied cross-user writes, anti-CSRF, blocked-user behavior, private accounts, no unsafe GET writes |
| S6. Application-layer hardening | Input/output encoding, uploads/URL validation, security headers, CSP in report-only then reviewed enforcement, bounded external API requests | Security tests, no secrets/log PII, no XSS regression, mobile/desktop UI parity |
| S7. Deployment + operations | Runtime SQL least privilege for production; separate migrations; secrets externalized, rotation and access audits; dependency vulnerability checks, release provenance | SQL/Docker audits, backup **off host**, restore drill, patch/update plan, monitoring, alerting, no exposed DB port |
| S8. Final product acceptance | All existing and planned features integrated: games, people, company, home, library, Steam, recommendations, search, profiles, email, social and settings | Full browser journeys, IGDB human calibration, load tests, permission tests, failed-upgrade rollback rehearsal and explicit owner sign-off |

### Present state (do not inflate)
Model and migrations, isolated SQL tests, Identity account services and staged
page handlers **exist in the canonical code**. Many are **not wired into
the running Gamefilled app**. The preview currently retains legacy
session/BCrypt authorization. Green CI for staged services **is not** a
secure production cutover. Cross-site penetration testing and full E2E
approval remain outstanding.

### Independent security audit at completion
Before any public launch, run a targeted checklist based on OWASP ASVS:
authentication and session lifecycle, CSRF, XSS, SQL injection, rate limits,
mass assignment, IDOR, SSRF, email token replay, privilege boundaries,
logging, secret handling, dependency vulnerabilities, network exposure,
backup restore and incident-response steps. An external review is beneficial
for internet-facing deployment. **No software is guaranteed vulnerability-free.**

## Phase R — FINAL code/structure overhaul (only after S0–S8)

When the security roadmap is complete and the owner has validated the product,
reorganize the implementation without changing functionality or UI:

1. Lock in baseline contracts: same routes, HTML classes, design tokens,
   critical IGDB relevance datasets, SQL identifiers, API shapes, and
   database migration history.
2. Enforce naming conventions: `PascalCase` for C# types/methods/properties,
   `camelCase` locals/parameters, coherent namespaces and service interfaces;
   no ambiguous `V2`, `Legacy`, `Temp`, `New2` names left active.
3. Move by feature boundaries: `Features/Accounts`,
   `Features/Games`, `Features/Library`, `Features/Profiles`,
   `Features/Social`, `Features/Notifications`,
   `Features/Companies`, `Infrastructure/Persistence`,
   `Infrastructure/Email`, `Infrastructure/Igdb`,
   `Web/Pages`, and `Web/wwwroot` (or equivalent after review).
4. Centralize validation, normalized DTOs, error handling,
   authorization and application services; separate database entities from
   public API/view models. Eliminate duplicated classes and dead prototypes
   only after usage/reference and data-migration checks.
5. Smaller, independently reviewable PRs with snapshots, contract tests,
   migration diff checks and browser comparisons after **every** move.
6. Remove historical BCrypt/session routes, unused migrations/build artifacts,
   abandoned prototypes, and old naming only after confirming no runtime,
   backup/restore or external API dependency. Preserve needed Git history.
7. Produce concise architecture docs, ADRs, service dependency map,
   dev setup, security operations runbook, release and rollback instructions.

**Refactor completion is not a rewrite.** Zero unintended frontend, route,
database schema or IGDB behavior changes is the acceptance criterion.
