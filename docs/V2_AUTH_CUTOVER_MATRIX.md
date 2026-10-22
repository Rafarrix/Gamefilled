# Gamefilled V2 — single-Identity cutover inventory

**Status (22/10/2026): migration preparation, not a completed auth migration.**
The real V2 product is the canonical `Gamefilled.csproj`, `Pages/**`, and
`wwwroot/**`. The working private preview on `docker-01` hosts this
original UI with an independent SQL database. `web-01` is not to be touched
before the project owner accepts the complete product.

The V2 modernization prototype under `v2-modernization/Gamefilled.V2`
has tested Identity, confirmed-email, password-reset and GUID-library
logic, **but is a separate app**. Do not represent those tests as proof
that the actual Gamefilled currently uses Identity.

## Why the change must be coordinated

The original `Users` table uses `int` keys and custom BCrypt password
hashing. Login writes account authority into ASP.NET Core Session, and
profile, follows, activity, reviews, notifications, library and settings
read from that session or resolve usernames into old `int` user IDs.
The modern Identity prototype uses GUID keys and secure cookie claims.
Changing only login would create conflicting states and bypass risks.
Never authorize mutations using a "try Identity, else legacy session"
fallback.

## Measured account/session migration map

An xUnit test in `tests/Gamefilled.Tests/IdentityCutoverGuardTests.cs`
records a maximum number of old `Session.Get*/Set*/Clear/Remove`
calls per file. New session dependencies are forbidden; reductions are
encouraged. This is **a migration guard, not a security certification**.

| Integration area | Existing account/session consumers | Required change |
| --- | --- | --- |
| Login + logout | `Pages/users/Login.cshtml.cs`, `Logout.cshtml.cs` | Standard Identity SignInManager + cookie logout, verified email, safe return URL |
| Registration + recovery | `Pages/users/Register.cshtml.cs`, currently only custom BCrypt; recovery exists only in prototype | UserManager, verification/resend/reset and signed HTTPS URLs, keeping the existing branded forms |
| Global UI | `Pages/Shared/_Layout.cshtml`, `Pages/Index.cshtml.cs` | Read identity claims, preserve current header, hero and responsive CSS |
| Games and saved library | `Pages/games/Game.cshtml.cs`, `Pages/u/Library.cshtml.cs`, `Pages/u/Activity.cshtml.cs`, `Pages/settings/FavoriteGames.cshtml.cs` | GUID ownership FKs and authenticated server-side authorization; no IGDB query calibration changes |
| Profile and social | `Pages/u/Profile.cshtml.cs`, `Pages/u/Friends.cshtml.cs`, `Pages/people/Index.cshtml.cs`, `Pages/users/Follow.cshtml.cs`, `Followers.cshtml.cs`, `Following.cshtml.cs` | One canonical Identity user ID; authorized follow/visibility and privacy boundaries |
| Notifications | `Pages/api/NotificationUnread.cshtml.cs`, `Infrastructure/ProtectedPageModel.cs` | Claims principal ownership, authenticated API; preserve POST-only CSRF notification actions |
| Settings | `Pages/settings/Account.cshtml.cs`, `Blocked.cshtml.cs`, `Defaults.cshtml.cs`, `Infractions.cshtml.cs`, `Integrations.cshtml.cs`, `Index.cshtml.cs`, `Notifications.cshtml.cs`, `Profile.cshtml.cs` | Protected policies; email changes confirmed at the replacement inbox and secure profile edits |
| Session infrastructure | `Infrastructure/RequireLoginFilter.cs`, `Application/Social/UserPresenceMiddleware.cs` | Claims and Identity security stamps; separate user presence from authentication authority |

**Additional requirements:** search for indirect user lookups in DbContext
queries, helpers and Razor Page handler arguments; tests cannot infer authorization
just from a syntactic session read. Exercise every route, direct forged POST,
CSRF failure, cross-user access, logout, password reset, account lockouts,
profile visibility, and cookie renewal. Public game/company discovery must
remain accessible anonymously as before.

## Database and migration gates

1. Generate/review a **new schema** for Identity users, library,
   follows, activities, reviews, notifications and settings using GUID
   FKs. Prefer one canonical EF Core DbContext rather than two permanently
   separate account models; the main preview currently uses
   `GamefilledFullPreview` and the standalone proof-of-concept uses
   `GamefilledV2Preview` — neither is the development `Gamefilleddb`.
2. Before activating Identity in the canonical runtime, prove a complete
   fresh migration and tests on isolated SQL; old test accounts do not
   need to migrate. Keep backups and rollback images. No migration on
   `web-01`.
3. Preserve the **same Razor templates**, route URLs and visual assets.
   Port the tested Identity flows into the main site; retire the old
   session authority, legacy BCrypt login and GUID-incompatible entities
   **together**, never with dual auth.
4. Verify every private read and write enforces the Identity user ID and
   anti-CSRF. Run all focused tests and a single complete browser regression
   across desktop and mobile, comparing with the original site.
5. Do not publish or cut over to the real environment without explicit
   final owner approval, a reviewed off-host backup, tested restore and
   user-confirmed acceptance of all features.

## Delivery

The first code change only adds the automated migration guard and this
inventory. It does **not** activate Identity and has no UI/runtime effect.
The next implementation branch can build the canonical GUID schema,
claim-based user-service authorization and branded account handlers together,
with integration SQL tests, before updating the preview for user acceptance.
