# Gamefilled V2 — active-feature parity gate (2026-10-24)

## Decision: discard throwaway PREVIEW accounts, NOT the site or features

Owner explicitly authorized discarding the private preview's **one** disposable
legacy user. Read-only audit confirmed **1 account** and **zero** game entries,
favorites, follows, activities and notifications in `GamefilledFullPreview`.
These are *only* counts for that separate preview SQL database, **not a report
about `web-01` or `Gamefilleddb`**.

**Strategy:** create an empty `GamefilledV2Integrated` database on `docker-01`,
apply the tested GUID Identity migration and register fresh test accounts.
Do not import any int users, legacy BCrypt hashes or old session identifiers.
Keep the current `GamefilledFullPreview` database and its volume unchanged
until feature-parity and rollback testing are finished; never run
`docker compose down -v`. Discard test users by retiring that obsolete
preview DB *only after* an explicit, verified final cutover and rollback gate.
Real-site account migration (if there are any users) is a **separate future
assessment** and cannot assume data is disposable.

## Old site functionality → integrated V2 status

**Definitions:** `staged` = the code exists but the public Razor handler
still uses the legacy session DB; `missing` = requires actual
implementation; `preserve` = remains in the canonical Gamefilled original
with UI and behavior unchanged. No staged feature is called "live".

| Product behavior in original site | Relevant main-app route / service | Integrated Identity backend state |
| --- | --- | --- |
| Home hero, artwork, logo, responsive dark UI | `Pages/Index`, `Pages/Shared/_Layout`, `wwwroot/**` | **Preserve exactly**, update authenticated header at atomic cutover |
| IGDB discovery, search, categories, company, game libraries, game details, TimeToBeat | `Pages/Search`, `games/**`, `company/**`, `api/Igdb*`, `Application/Games/**` | **Preserve original algorithms**; test relevance manually before any search changes |
| Email/username login, registration, logout | `Pages/users/Login`, `Register`, `Logout` | Identity services and PageModels **staged**, public routes still legacy |
| Verified email, resend, reset password, cookie revocation | `Application/Identity/**` | Identity implementations SQL-tested, **not routable yet** |
| Account/profile bio, avatar/banner, privacy, password and email change | `Pages/settings/Profile`, `Account` | Identity services **staged**, original forms need binding + end-to-end tests |
| Game status played/playing/backlog/wishlist and user ratings/reviews/spoilers | `Pages/games/Game`, `UserGameEntries` | GUID owner write service **staged**; saves activity with original event types |
| Home/profile activity and following timeline | `Pages/u/Profile`, `u/Activity`, `UserActivities` | Activity writes now **staged**; GUID read models + original UI handlers still missing |
| Public library status tabs, status counts, cover art, IGDB titles, search, sort and pagination | `Pages/u/Library`, `UserLibraryService` | Owner write service **staged**; **public read/card and filter parity missing** |
| Favorite-game editor: 1-5 ordered items, one highlighted favorite, IGDB covers | `Pages/settings/FavoriteGames`, profile favorites | GUID model exists; **validated edit/read service and page binding missing** |
| Profile social cards, friends/mutuals, followers/following lists | `Pages/u/Friends`, `users/Followers`, `Following`, `Application/Social/**` | GUID follow service **staged**; **full lists, presence, card parity missing** |
| Player directory with online/following/mutual filters | `Pages/people/Index` | **GUID-based filters, page counts and block/privacy rules missing** |
| Follow/unfollow toggles, follow activity and mutual notifications | `Pages/users/Follow`, `NotificationService` | GUID follow/unfollow/blocks **staged**; original activity and notification events now implemented |
| Notification bell preview, unread counts, inbox read/unread filters, open/mark-all | `Pages/Notifications`, `api/NotificationUnread` | GUID notification count, preview, mark-read, mark-all **staged**; **inbox paging/filters and Razor binding missing** |
| Online status / last seen | `UserPresenceMiddleware`, profile and people cards | GUID `LastSeenAtUtc` model exists; **claims-based presence + online lists missing** |
| Block management | `Pages/settings/Blocked` | **Original is a placeholder**, GUID block service staged; build actual list/UI separately |
| Steam integration | `Pages/settings/Integrations` | **Original is a placeholder**, defer OpenID/Steam library until account cutover complete |
| Notification delivery prefs / quiet hours | `Pages/settings/Notifications` | **Original is a placeholder**, do not fabricate working preferences |
| Personal defaults / infractions | `Pages/settings/Defaults`, `Infractions` | **Original placeholders**, no functionality to migrate yet |
| Legacy settings index demo save | `Pages/settings/Index` | **Original placeholder**; never carry over fake success into Identity implementation |
| General About/Contact/Terms/Privacy/Error | `Pages/About`, `Contact`, `Terms`, `Privacy`, `Error` | **Preserve** pages/links, review privacy/terms during public launch |
| User feedback / IGDB search calibration | Discovery queries and filters | **Manual calibration later**, change nothing automatically |

## Release/cutover acceptance — no exceptions

1. Separate empty `GamefilledV2Integrated` SQL initialized on preview using
   dedicated setup credentials and reviewed migrations, *without deleting or
   even altering `GamefilledFullPreview`*.
2. Fill remaining **active-feature parity** gaps above in integrated
   `Gamefilled.csproj` and ensure no fake placeholders claim to work.
   Preserve exact route links, original HTML/CSS/assets and IGDB query behavior.
3. Convert **every real Razor PageModel**, shared layout, middleware and
   API to use the **one Identity principal** and only GUID-owned entities.
   Remove BCrypt/session authority and legacy fallback atomically; enforce
   this with CI and SQL/browser negative tests.
4. Real browser journeys: new registration + confirmed email, logout and
   session revocation, password reset, library statuses and reviews, social,
   favorites, notifications, profile privacy, directory filters, mobile UI
   and all public IGDB browsing. Verify blocking and forgery protection.
5. Cut over **preview only** after snapshot/rollback rehearsal. Keep old DB
   detached and preserved until accepted. Old test account must NOT be able
   to sign in via any fallback, and newly created Identity accounts must.
6. User approves complete preview; only then consider production migration
   (with separate real-data audit), encrypted off-host backups, tested restore,
   network controls and explicit deploy approval.
7. After security roadmap, perform requested modular renaming/restructure
   overhaul in small test-backed PRs. IGDB result-quality calibration and
   personalized recommendations are later separate product-quality work.

**Critical:** No single-service CI passing means the preview has switched
to Identity. The preview currently runs the legacy login. Do not deploy
these staged-only changes expecting the login to have switched.
