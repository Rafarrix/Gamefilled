# Gamefilled V2 UI Audit

This document is the working checklist for the full visual and interaction audit.

## Non-negotiable rules

- Every page background must merge with the global `#0e1015` canvas. Page wrappers must not create visible strips or container-sized background blocks.
- Solid bright-green buttons are reserved for exceptional primary actions. The default action style uses dark surfaces with a restrained green border/accent.
- Product copy must be concise and action-focused. Avoid privacy, roadmap or explanatory paragraphs scattered through normal pages.
- All user-facing copy remains in English until localization is implemented.
- Desktop, ultrawide, tablet and mobile must each be reviewed before a route is marked complete.
- Empty, loading, partial-data and error states must use shared components.
- Page-specific CSS should not redefine global colors, button styles or content widths.
- A redesign is not automatically an improvement. Preserve an existing composition when it is clearer or more distinctive.

## Review states

- `Stable`: visually reviewed and functionally confirmed.
- `In audit`: currently being standardized in PR #7.
- `Pending`: still uses legacy or inconsistent presentation.

## Core routes

| Area | Routes | State | Notes |
| --- | --- | --- | --- |
| Home | `/` | Stable / recheck | Recheck ultrawide spacing and logged-in states. |
| Game discovery | `/games`, `/games/lib/*` | Stable / recheck | Recheck empty and IGDB failure states. |
| Game details | `/games/{id}` | Stable / recheck | Recheck media room, universe, timeline and personal actions on mobile. |
| Companies | `/company`, `/company/{id}/{slug}` | Stable / recheck | Recheck search errors and long company names. |
| People | `/people` | Stable / recheck | Recheck filters and cards on narrow screens. |
| Profiles | `/u/{username}` | Stable / recheck | Recheck empty libraries, reviews and long bios. |
| Social lists | followers, following, friends | In audit / runtime check | Shared People cards, presence and mutual state are implemented. |
| Authentication | Login, Register, Logout | In audit / runtime check | Established composition restored; only responsive and accessibility polish remains. |
| Settings | Overview, Profile, Favorite Games, Account | In audit / runtime check | Uniform background, widths, functional account forms and compact actions. |
| Planned settings | Notifications, Integrations, Privacy | In audit | Clearly planned; no fake save controls. |
| Search | `/Search` and header autocomplete | In audit / runtime check | Results, no-results and IGDB failure states now share the V2 language. |
| Notifications | `/notifications` | Pending | Audit list density, unread state and empty state. |
| Library pages | Played, Playing, Backlog, Wishlist | In audit / runtime check | Unified `UserGameEntries` page with search, sorting and pagination. |
| Reviews / Activity / Lists / Journal | profile subpages | Pending | Establish shared profile subpage shell. |
| Static content | About, Contact, Terms, Privacy | Pending | Use editorial shell and readable typography. |
| Errors | 404, 500, IGDB/database failures | Pending | Shared error component with safe technical detail. |

## Current PR #7 validation

### Authentication and settings

- [x] Login desktop uses the established pre-audit composition.
- [x] Register desktop uses the established pre-audit composition.
- [ ] Authentication works on mobile without horizontal scrolling.
- [x] Settings background is visually continuous with the body.
- [ ] Settings scales appropriately on 1440p and ultrawide monitors.
- [x] Favorite Games feels like part of Settings.
- [x] Favorite Games search, drag-and-drop, remove and crown actions still work.
- [x] Save actions are compact and use standardized buttons.
- [ ] Email change validates current password and duplicate emails in local runtime.
- [ ] Password change validates current password and confirmation in local runtime.

### Social lists

- [ ] Followers shows avatar, bio, presence and mutual badge correctly.
- [ ] Following shows avatar, bio, presence and mutual badge correctly.
- [ ] Friends, Followers and Following agree on mutual status and online state.
- [ ] Empty social lists use the shared state without background strips.

### User libraries

- [ ] `/u/{username}/games` displays Played entries.
- [ ] `/u/{username}/playing` displays Playing entries.
- [ ] `/u/{username}/backlog` displays Backlog entries.
- [ ] `/u/{username}/wishlist` displays Wishlist entries.
- [ ] Counts remain consistent across profile, tabs and database entries.
- [ ] Search, Recent, Title, Rating and Release sorting work.
- [ ] Pagination preserves search and sorting.
- [ ] Missing IGDB covers and metadata degrade safely.
- [ ] Two-column mobile grid and ultrawide six-column grid feel deliberate.

### Search

- [ ] Empty query, no-results and IGDB-error states render correctly.
- [ ] Top result and remaining cards open the correct game.
- [ ] Long names and games without covers do not break the layout.
- [ ] Search works on mobile without horizontal scrolling.

## Next implementation slice

1. Notifications.
2. Reviews, Activity, Lists and Journal.
3. Error and static pages.
4. Footer and global responsive pass.
5. Remove superseded CSS and duplicate selectors.
6. Final route-by-route screenshot review.
