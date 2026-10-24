# Legacy Gamefilled → Unified Identity: preflight and zero-loss migration

## Read-only readiness audit

Use **only** the separate Gamefilled preview VM with running `GamefilledFullPreview`
and the restricted SQL account `gf_preview_app`. The audit container has no
route, no public port, no mounted host volumes, and no administrative SQL
credentials. Its source performs aggregate `SELECT COUNT_BIG` queries only.
Do not send emails, usernames, password hashes or DB credentials in logs.

```bash
cd /opt/gamefilled-preview/repo
git pull --ff-only origin master
bash preview/previewctl.sh audit
```

The command **does not change the site, schema or account data**. Output
contains anonymous aggregate counts for accounts, library entries, favorites,
follows, activities and notifications, and red flags for mismatches.

### Why this audit is needed

The old site uses `Users.Id int` with manually generated BCrypt hashes;
the unified V2 uses `AspNetUsers.Id Guid` and standard Identity security
stamps/tokens. Existing legacy accounts **must not be silently discarded**
and old login handlers cannot remain as a fallback alongside Identity.

Known structural differences that are intentionally flagged:

- Usernames up to 100 chars can exist in legacy; new signup permits **3–30
  ASCII letters/numbers/underscore**. Unicode/long/empty usernames or
  case-normalized collisions need explicit resolution, not truncation.
- Legacy email entries were not necessarily verified. **Never set
  `EmailConfirmed = true` merely because an address was in the old database**.
- `Users.Role` may contain privileged values. Don't automatically map the
  word `Admin` to an Identity role without a reviewed authorization policy.
- Password hashes are currently BCrypt. **Never copy a BCrypt string
  directly into Identity's default PasswordHash field.** Identity cannot
  verify it with its normal hasher; carry out a verified account recovery
  flow or implement a tightly scoped, audited rehash-on-login strategy
  followed by permanent removal of legacy verification. New accounts use
  Identity hashing only.
- Legacy `UserActivities.MetaJson` is unbounded `nvarchar(max)` but the
  draft new schema caps it at 4000 characters; preserve or explicitly
  reconcile all longer historical activity payloads.
- `UserFavoriteGames.CreatedAt` exists in legacy but is missing from the
  first unified favorites schema. Until a reviewed schema migration preserves
  this timestamp, **all existing favorites count as a blocking mismatch**.
- Redirect URLs on old notifications might be external; never follow them
  without checking for a safe local path.

### Next implementation steps — no writes until accepted

1. Run this count-only audit in the preview and review **all blockers**.
   A zero count is *not* proof that records can safely migrate.
2. Extend the **new V2 schema** to preserve all required legacy profile,
   favorites, activity and social metadata and store a durable
   `LegacyIntUserId → new Guid` mapping. Do not derive GUIDs from public
   integer IDs; generate random GUIDs once and persist mapping in staging.
3. Implement a copy into an **isolated new database**
   `GamefilledV2Integrated`. Import users, games, reviews, favorites,
   follows, activities and notifications without deleting source rows.
   Verify field-by-field parity, user ownership, row counts and timestamps.
4. Do not trust migrated email addresses; use verified activation/reset for
   ownership or a carefully reviewed password migration. No temporary
   authorization fallback on old session IDs or old password hashes.
5. Re-run the copy on a frozen source snapshot to verify idempotency.
   Exercise rollback and restore; never cut over with concurrent live writes.
6. Replace all real PageModels/routes and middleware with the canonical GUID
   Identity principal, then delete old Session/BCrypt authority **only after**
   tests show there are no references.
7. Run full Gamefilled browser and security regression plus the user's manual
   IGDB/result-quality validation before any real-site deployment.

**This audit is not an import or an Identity deployment.** The existing
preview users can continue signing in through the legacy auth until final
cutover, while the old production/development environment stays unchanged.
