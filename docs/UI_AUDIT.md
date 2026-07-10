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
| Social lists | followers, following, friends | In audit | Reuse People/Friends card language everywhere. |
| Authentication | Login, Register, Logout | In audit | Restore established composition and remove unnecessary copy. |
| Settings | Overview, Profile, Favorite Games, Account | In audit | Uniform background, widths and action components. |
| Planned settings | Notifications, Integrations, Privacy | In audit | Clearly planned; no fake save controls. |
| Search | `/Search` and header autocomplete | Pending | Standardize results, no-results and service failure states. |
| Notifications | `/notifications` | Pending | Audit list density, unread state and empty state. |
| Library pages | Played, Playing, Backlog, Wishlist | Pending | Replace legacy placeholders with `UserGameEntries`. |
| Reviews / Activity / Lists / Journal | profile subpages | Pending | Establish shared profile subpage shell. |
| Static content | About, Contact, Terms, Privacy | Pending | Use editorial shell and readable typography. |
| Errors | 404, 500, IGDB/database failures | Pending | Shared error component with safe technical detail. |

## Current PR #7 validation

- [ ] Login desktop matches the established pre-audit composition.
- [ ] Register desktop matches the established pre-audit composition.
- [ ] Authentication works on mobile without horizontal scrolling.
- [ ] Settings background is visually continuous with the body.
- [ ] Settings scales appropriately on 1440p and ultrawide monitors.
- [ ] Favorite Games feels like part of Settings.
- [ ] Favorite Games search, drag-and-drop, remove and crown actions still work.
- [ ] Save actions are compact and use standardized buttons.
- [ ] Email change validates current password and duplicate emails.
- [ ] Password change validates current password and confirmation.
- [ ] All current UI copy is English and concise.

## Next implementation slice

1. Followers and Following cards.
2. Played, Playing, Backlog and Wishlist.
3. Search and autocomplete states.
4. Notifications.
5. Error and static pages.
6. Footer and global responsive pass.
7. Remove superseded CSS and duplicate selectors.
