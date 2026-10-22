# V2 integration contract — preserve the actual Gamefilled

## Product decision (confirmed by browser comparison on 2026-10-22)

Gamefilled V2 means the post-PAP product whose actual UI lives in the main
`Gamefilled.csproj` application. The existing development site's homepage,
nav, game/people/company discovery, game details, profiles, library, account
pages, cards, transitions and responsive visual identity are the baseline.

The technical `v2-modernization/Gamefilled.V2` ASP.NET Core app is **only an
isolated backend experiment**. It proves certain Identity, email, library and
SQL flows work; it does NOT provide the existing frontend or feature parity.
The Docker Compose project currently used at `preview.gamefilled.pt:8444`
builds this experiment. HTTPS smoke tests prove operational health **only**.

**Desired acceptance:** visiting the final private preview must show the
**real Gamefilled** (visually and functionally) with reviewed backend changes
integrated into **that same application**. It is not acceptable to replace
the product with simplified pages, duplicated templates or a new starter UI.

## Canonical implementation

- **App to evolve:** `Gamefilled.csproj`, `Program.cs`, `Pages/**`, `wwwroot/**`.
- **Design sources:** `Pages/Shared/_Layout.cshtml`, `Pages/Index.cshtml`,
  `Pages/users/Login.cshtml`, `Pages/users/Register.cshtml`, game/company/people
  pages, and their current CSS/images/JavaScript in `wwwroot`.
- **Reusable backend proof-of-concept:** `v2-modernization/Gamefilled.V2/**`
  (Identity models and EF migrations, verified email, password reset,
  authenticated library/reviews, profile authorization and security tests).
- **Database:** a new, isolated schema for the integrated preview; legacy
  `Gamefilleddb` remains untouched. Existing preview-only test data is not a
  replacement for the actual product database. No legacy-session fallback
  authorizing writes alongside Identity; test data need not migrate.
- **Runtime:** `docker-01` may continue hosting the independent preview stack;
  `web-01` with the current development site remains untouched until a separately
  authorized cutover. No public DNS, port forwarding or WAN exposure.

## Integration work, in order

1. **Create an integration branch rooted in the current main app**. Inventory
   every route, Razor handler, user-ID/session dependency and mutation, including
   layout/header and JavaScript APIs. Protect all existing visual assets. Create
   an isolated combined-preview SQL migration plan and test it on an empty DB.
2. **Identity in the real app:** port/reuse the tested Identity backend, with
   **the existing** login/register page layouts and URLs retained (or
   compatible redirects). Header, auth badges and profile links must use a
   single authenticated principal. Enable verified email, logout, password
   reset and proper antiforgery. Replace session authority entirely.
3. **Library and game details:** keep the existing game detail / discovery
   templates, media room, universe, covers and interactions. Port secured
   library/review mutations to Identity GUID FKs. Do not rewrite IGDB relevance,
   special categories, sort or filters without human relevance testing.
4. **Community and settings:** adapt existing profile, people, follows/friends,
   activities, notifications, settings and APIs to Identity. Ensure owner/visitor,
   privacy and block boundaries; test no GET mutation and no cross-user write.
5. **Steam and recommendations:** only after integrated account/library/social
   paths work; optional Steam linking/import separate from manually tracked
   statuses; FY homepage section **within** existing `gf-home-section` cards.
6. **Actual private preview:** adjust the preview container build to publish the
   **integrated Gamefilled app**, on isolated SQL and private HTTPS, not just
   `v2-modernization/Gamefilled.V2`. Validate it with real-browser tests
   against the current development site at identical viewport sizes. No
   prod cutover without a restorable off-host backup and express approval.

A code-complete feature confined to the isolated backend laboratory is
**implemented experimentally**, not **integrated in Gamefilled**. Mark it
that way in PRs, issue status and release notes.

## Visual and functional no-regression gate

On **desktop (e.g. 1440 px)** and **mobile (390 px)** compare the same
unmodified product pages with before/after screenshots (controlled content):

| Route / journey | Baseline to preserve | Backend improvement |
| --- | --- | --- |
| `/` | Existing artwork collage hero, header, CTA, Trending, New and fresh, cards | Principal-aware home, later FY cards in same design |
| `/games`, `/games/{id}` | Filters, sorting, covers, game media and details | Typed services, guarded writes, Identity-owned reviews |
| `/people`, `/company` | Current card layouts, directories and links | Secure queries, caching and validated actions |
| `/users/Login`, `/users/Register` | Current large-logo/background/form compositions | Identity, confirmed email, rate limits, recovery |
| `/u/{username}`, library routes | Profile shell, stats, activity, library status | Identity GUID ownership and privacy |
| Notifications and settings | Existing header/menu, meaningful working views | POST-only state changes, authorization, proper validation |

For each route check links, content, interactive behavior, auth status,
keyboard focus, images, responsive layout, database isolation, exceptions and
network failures. A green CI build, HTTPS 200 or visually pleasing *new*
mockup is **insufficient**. Do not mark a route complete until the owner has
reviewed real-browser results and approved the change.

## Immediate state and next milestone

The private technical lab is running; do not confuse it with the complete
product preview. Retain it only for backend-specific tests. The **next product
milestone** is an integrated, independently deployable preview of the main
Gamefilled application preserving `Pages/Index.cshtml` and
`Pages/Shared/_Layout.cshtml`, with Identity/auth and library migrated in
coherent slices. Never deploy an intermediate dual-auth app to public users.
