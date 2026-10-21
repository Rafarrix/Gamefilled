# Gamefilled V2 — product & security backlog

The V1 PAP presentation is historical. **Only one Gamefilled V2** will be
deployed after the modernization cutover. The isolated
`v2-modernization/` host exists for development/testing, not as a permanent
second production website. Retire the legacy custom-session backend only
after the modernized modules have feature parity and pass acceptance tests.

## P0 — required before any public release

| Area | Required acceptance |
| --- | --- |
| Accounts | Identity register/login/logout, verified email, resend, secure forgot/reset/change password, revocation, protection against account enumeration and credential stuffing |
| Data | New SQL database, EF migrations in CI and staging, least privilege runtime DB account, encrypted off-host backup and **tested** full restore |
| Authorization | Every library/review/profile/follow/notification mutation verifies the authenticated user and ownership; CSRF for all authenticated browser mutations |
| Community safety | Block/report flows, basic moderation queue, admin role separation and audit trail for moderation actions |
| Privacy | Users can view, export, correct or delete personal data; retention/deletion rules and working Privacy/Terms/Contact pages |
| Platform | TLS, safe host/proxy trust, limits for IGDB + account endpoints, safe file/image URLs, logs without credentials/PII, patch/dependency scan and monitored resource limits |
| Ops | Backup/restore and Data Protection key retention, staged rollout/rollback, production secrets and outbound email quotas |

## P1 — valuable product improvements after core works

- **Custom game lists:** public/private collections, e.g. best RPGs, games to
  finish, co-op recommendations, with ordered entries.
- **More useful library:** progress/achievement notes, playing/paused/completed
  status, completion date, play time, replayed and quick filters/sorting.
- **Recommendations/discovery:** platform, genre, rating and friends filters;
  cache/throttle IGDB instead of querying upstream for every visit.
- **Reviews and community:** spoiler flags, review drafts, helpful reactions,
  configurable notifications, anti-spam limits and profile privacy controls.
- **User preferences:** export CSV/JSON, timezone/locale, privacy defaults,
  option to hide activity or block users.
- **Mobile and accessibility:** responsive secondary pages, keyboard navigation,
  meaningful contrast/focus states, reduced-motion support.

## P2 — optional only after measuring demand

- **Passkeys/WebAuthn or MFA:** recommended for admins early; user opt-in
  after a secure account-recovery workflow and verified supported providers.
- **OAuth sign-in:** e.g. Microsoft/Google, only if demanded and after testing
  secure external identity linking and callback handling.
- **Sessions/devices dashboard:** list active sessions and revoke sessions;
  requires server-side session records instead of a misleading UI.
- **Social sharing & personal stats:** annual recaps, backlog charts,
  collections and cross-platform share cards.

Avoid microservices, paid recommendations infrastructure, AI-generated
profile data or unmoderated uploads until there is a concrete requirement.

## Design principles

1. Use maintained framework services and least-privileged dependencies instead
   of custom authentication/security crypto.
2. A UI element is not a feature until the backend, database, permissions,
   error handling and tests work.
3. Prefer modular monolith services and migrations over premature microservice
   complexity. Structure modules so they can be extracted later if needed.
4. Avoid storing unnecessary personal data. Never include passwords,
   confirmation/reset tokens, mail API keys or session cookies in logs.
5. Keep the existing V2 database/containers running privately until one
   explicitly authorized, tested cutover replaces them.
