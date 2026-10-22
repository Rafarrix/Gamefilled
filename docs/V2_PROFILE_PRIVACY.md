# Gamefilled V2 — private Identity profile foundation

This increment is the **first community/profile migration slice**, not the
launch of a public directory, follow system or activity feed.

- New authenticated-only \`/profile\` page uses the current Identity GUID.
- It shows only the viewer's account username, optional display name,
  account creation month and library status counts.
- Library totals are queried by the authenticated user's GUID on the server;
  no user ID or email is accepted from URL/form, and no public API is added.
- Optional display name is trimmed, restricted to 100 characters and written
  through Identity's UserManager. Razor encodes the rendered name.
- Edit uses POST + Razor antiforgery and a per-user write rate limiter.
- Cache-Control: private, no-store; no new database columns/migrations.
- A separate SQL/HTTP test validates anonymous denial, A/B isolation, CSRF,
  oversize input rejection and HTML encoding.

**Privacy baseline:** All profiles and game libraries remain private during
this preview. Future public \`/u/{username}\` profiles and \`/people\` discovery
must have explicit opt-in visibility and blocking policies before exposure.

Next: define opt-in public profile privacy/migration, safe profile projection,
Identity GUID follow edges with uniqueness/concurrency tests, then activity,
notifications, blocking and moderation. Preserve the old design and run full
cross-module acceptance checks before cutover.

No Proxmox/Compose/DNS changes and no changes to IGDB queries.
