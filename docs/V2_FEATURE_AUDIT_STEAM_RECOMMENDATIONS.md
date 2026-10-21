# Gamefilled V2 — Feature audit, Steam sync, FY recommendations and cutover

Status: **source-code audit and feature acceptance specification**.
This audit examined the existing V2 Razor Pages, services and DTOs on
2026-10-21. It is NOT a complete runtime pentest or end-to-end functional
verification. All status labels below are based on repository code inspection.

## Product decisions (approved direction)

- **V1 = PAP presentation only. V2 = all post-PAP work.**
- **One final V2 application**: `v2-modernization/` is a temporary development
  foundation, not a second long-term production website. The current V2
  remains isolated during the migration; delete obsolete implementation only
  after passing the acceptance tests and restoring a backup in staging.
- Preserve the existing Gamefilled design language; polish secondary pages
  using existing typography, card/grid patterns, spacing and navigation.
- Personalization is a **'For You' (FY)** section on the homepage that visually
  follows the existing **'New and fresh' / Recent Releases** section pattern.
  Do not replace the "New and fresh" section.
- Add similar/recommended game cards to each game-detail page.
- Rebuild and verify the **community** (follows, friends, activity,
  reviews, notifications, visibility) as a priority **before moderation**;
  moderation is required prior to public community launch but is planned
  after community and functional parity.
- After modular implementation, perform a **single comprehensive final
  regression pass** across all user journeys, while still running focused
  tests in every PR to find defects early.

## Verified existing source code and action

| Area | Evidence in current V2 | Required change and acceptance |
| --- | --- | --- |
| Homepage discovery | `Pages/Index.cshtml` has `gf-home-section`, `New and fresh`, `games-grid`, `game-card`; `Index.cshtml.cs` loads trending/recent/top-rated | Add FY block with **same layout components**, hide or show generic recommendations for anonymous/cold-start users, never leak private library |
| Game detail | `IgdbGameDetailsDto.SimilarGames` and `IgdbClient.GetGameDetailsAsync` already query `similar_games`; detail page renders `universeSections` for editions/related games | Reuse IGDB `similar_games`, render a dedicated **Games you may like** block matching current cards; exclude current ID/duplicates, stable ordering, bounded results and empty/fallback state |
| Personal library | `UserLibraryService` and `UserGameEntry` store statuses `played`, `playing`, `backlog`, `wishlist`, ratings/review | Integrate Steam ownership as a **separate attribute/link**, not an automatic "played" or "backlog" label; preserve manual states and reviews; protect all writes by Identity user FK |
| Steam connectivity | Not yet implemented in repository | Steam OpenID 2.0 account linking; Web API owned game import with explicit user consent, exact SteamAppID→IGDB mapping and import preview before committing |
| Game activity | `Pages/u/Activity.cshtml.cs` queries old user IDs and session username | Rebuild on current Identity account and activity authorizations; enforce profile visibility |
| Friends and follows | `Pages/users/Follow.cshtml.cs` posts session-based follow toggle; `SocialGraphService` computes mutual links | Rebuild on canonical GUID user IDs; tests for self-follow, cross-user writes, privacy/blocking, duplicate/race, follow/unfollow notifications |
| Game reviews | `Pages/games/Game.cshtml.cs` writes user game entries and review using old session account | Identity ownership checks, normalized ratings/status, spoiler handling, profanity/abuse safety and concurrency tests |
| Notifications | `Pages/Notifications.cshtml.cs` has `OnGetOpenAsync` that **marks a notification read** | Remove mutation from GET. Use POST-only + antiforgery; test no changes from GET, isolated unread count, no cross-user access and notification target safety |
| Notification count API | `Pages/api/NotificationUnread.cshtml.cs` gets `Session.GetInt32("userId")` | Derive authenticated GUID identity, return only viewer's count, cache-control no-store |
| Secondary settings | `Pages/settings/Blocked`, `Infractions`, `Defaults`, `Integrations` need complete functionality review | Implement or hide placeholders; Steam connection goes into Integrations with link/sync/unlink and privacy controls |
| Terms/privacy/contact | Pre-launch content in `Pages/Terms`, `Privacy`, `Contact` | Complete data handling disclosures, support and deletion/export workflows before public launch |

Source paths are relative to repository `Rafarrix/Gamefilled`.

## Steam import: authoritative mapping and privacy

1. Viewer must first be authenticated with **Gamefilled Identity**.
2. Viewer initiates "Connect Steam" in **Settings → Integrations**. Use
   Steam's official **OpenID 2.0** identity URL to verify the assertion on
   the server and obtain their SteamID64. Protect the linking transaction
   from CSRF/replay/open redirects; never request Steam username/password.
   OpenID sign-in identifies the account **but does not grant a special
   private-library access token**.
3. After user consent, call official `IPlayerService/GetOwnedGames`
   server-side with a permitted server API key and the linked SteamID.
   Respect visibility controls and any private games: inaccessible games
   must remain inaccessible; show a clear "Steam library not visible"
   state and optional manual entry. Do **not** advise circumventing privacy.
4. Use IGDB `external_games.uid` plus its authoritative Steam external
   game source to match exact AppIDs to Gamefilled IGDB game IDs. Do not
   auto-match by title, because regional versions/remasters/DLCs collide.
   Mark unrecognized or ambiguous IDs for review and optional manual match.
5. A **preview** lists matched, already linked, unmatched and duplicate
   games with playtime. The initial opt-in imports only verified ownership.
   Keep "Steam owned" separate from `played/playing/backlog/wishlist`;
   optional playtime is metadata, NOT proof of completion.
6. Import transaction uses unique `(GamefilledUserId, Provider, ExternalAppId)`
   and checked FKs. It is **idempotent**: retry/sync never creates duplicate
   library entries or overwrites manual ratings, reviews and statuses.
7. First sync is on user demand; later periodic sync requires clear consent.
   A disconnected Steam account stops sync. Expose disconnect + delete
   imported Steam metadata in account privacy controls. Never auto-delete
   local library on an empty/private Steam API response.
8. Store SteamID64 only for the linked authenticated user, not Steam
   credentials or session cookies. Keep the API key server-side and
   rate-limit / cache requests.

Steam docs:
- https://partner.steamgames.com/doc/features/auth
- https://partner.steamgames.com/doc/webapi/IPlayerService

## Recommendations UX and algorithm

### Home FY / For You

Place a separate section on `Pages/Index.cshtml` that reuses the layout
from **New and fresh**: `gf-home-section` heading/eyebrow,
`games-grid`, `game-card`, same cover fallbacks and responsive behavior.
Section names might be "For You" / "Picked for you".

First version: deterministic scores combining IGDB genres/themes/similar
relations with the viewer's own **voluntarily saved games**, reviews and
optional Steam ownership. Prefer not-yet-owned/not-in-library candidates,
allow discovery diversity, remove duplicate IDs, block adult/inappropriate
content as per product policy, and put a bounded, cached IGDB request budget
around candidate generation. New users get a transparent generic fallback
(e.g. Trending + well-rated recent titles), **not a fabricated personal
recommendation**. If user personalization is disabled, use generic results.

Recommendation inputs should not be exposed as other users' private data.
Log aggregate system metrics, not private per-user interests.

### Game detail — similar games

Use existing `similar_games` IGDB metadata; no new AI system required.
Show cards **under the game's existing content** with the current Gamefilled
style and accessible links. Deduplicate, exclude the current game and hide the
section gracefully if no verified related games exist. Cache with TTL.

## Community: one coherent test campaign

Focused tests per PR, then a consolidated browser + SQL integration campaign
on the full modernized V2 build, with at least three seeded users (A, B, C)
and distinct permissions. Use stubbed IGDB/Steam/Resend providers so tests
do not depend on external network uptime.

Test journeys:
1. Anonymous vs verified vs banned/deactivated vs ordinary vs admin access.
2. A follows B; B follows A; mutual friends and counts; A unfollows B.
3. A blocks B and verifies blocked feed/profile/follow/notifications.
4. A adds and removes a game; updates status, reviews and spoiler flags;
   B cannot edit A's entries.
5. Notifications generated exactly once, visible only to recipient; GET
   never mutates data; mark read via POST; unread count is correct.
6. Concurrent follow/import/review updates remain idempotent and respect
   uniqueness and ownership constraints.
7. Private activity, profile visibility, GDPR export/delete and Steam
   disconnect are respected by all feeds and API endpoints.
8. Mobile/desktop navigation, empty states, error states and accessibility.
9. Database migration from blank schema, complete backup/restore and
   rollback procedures on a **non-production** SQL Server.

### Release gate

Before cutover, run **all** acceptance suites together on a fresh SQL test
database, plus browser walkthrough and performance test. Review failures
per feature and repeat the full run after fixes. No public network exposure
until functionality, community privacy, moderate-abuse controls, secrets,
backups and security gates are satisfied. Existing V2 container stays live
privately until that point.

## Implementation order from here

1. Finish auditable canonical Identity account/session/email flows
   (already underway and SQL-tested).
2. Move **library + reviews + profile + community** to Identity user
   IDs, with security and behavior tests.
3. Build Steam connection, exact game mapping, import preview and
   opt-in sync; prove idempotency with SQL tests.
4. Add similar-games block and home FY block **using existing CSS/design**.
5. Perform comprehensive cross-feature regression testing.
6. Add abuse reports, blocks, moderation and legal/privacy features before
   any public launch.
7. Keep polishing secondary pages to match current Gamefilled design.

## Current scope of this PR

The initial checked-in Steam import planner is **pure code and tests only**.
It does NOT query Steam, store SteamIDs, link accounts, alter the database
or change any frontend. This minimizes regression risk while the Identity
user model and ownership schema are being migrated.
