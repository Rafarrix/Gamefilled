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
- Provider attribution belongs in global attribution areas such as the footer, not inside normal user actions or discovery copy.
- Personal routes should speak directly to the owner (`your library`, `your friends`, `you rated`) while remaining readable for visitors viewing another profile.

## Review states

- `Stable`: visually reviewed and functionally confirmed.
- `In audit`: currently being standardized in PR #7.
- `Pending`: still uses legacy or inconsistent presentation.

## Core routes

| Area | Routes | State | Notes |
| --- | --- | --- | --- |
| Home | `/` | Stable / recheck | Recheck ultrawide spacing and logged-in states. |
| Game discovery | `/games`, `/games/lib/*` | Stable / recheck | Recheck empty and service failure states. |
| Game details | `/games/{id}` | Stable / recheck | Recheck media room, universe, timeline and personal actions on mobile. |
| Companies | `/company`, `/company/{id}/{slug}` | Stable / recheck | Recheck search errors and long company names. |
| People | `/people` | Stable / recheck | Recheck filters and cards on narrow screens. |
| Profiles | `/u/{username}` | Stable / recheck | Recheck empty libraries, reviews and long bios. |
| Social lists | followers, following, friends | In audit | Personal owner copy and shared People/Friends cards implemented. |
| Authentication | Login, Register, Logout | In audit | Established composition restored; concise copy only. |
| Settings | Overview, Profile, Favorite Games, Account | In audit | Uniform background, widths and action components. |
| Planned settings | Notifications, Integrations, Privacy | In audit | Clearly planned; no fake save controls. |
| Search | `/Search` and header autocomplete | In audit | Gamefilled catalog copy and shared result/error states implemented. |
| Notifications | `/notifications` | Pending | Audit list density, unread state and empty state. |
| Library pages | Played, Playing, Backlog, Wishlist | In audit | Real `UserGameEntries`, personal copy, sorting and pagination implemented. |
| Activity | `/u/{username}/activity` | In audit | Personal timeline and game-aware activity cards implemented. |
| Reviews / Lists / Journal | profile subpages | Pending | Confirm real data before building visible feature shells. |
| Static content | About, Contact, Terms, Privacy | Pending | Use editorial shell and readable typography. |
| Errors | 404, 500, service/database failures | Pending | Shared error component with safe technical detail. |

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
- [ ] Followers, Following and Friends use personal copy for the profile owner.
- [ ] Profile social metrics open dedicated list pages.
- [ ] Played, Playing, Backlog and Wishlist counts match the profile.
- [ ] Library search, sort and pagination preserve the selected section.
- [ ] Library hero balances title, owner identity and total on ultrawide/mobile.
- [ ] Search contains no provider-facing product copy.
- [ ] Footer attribution is visible but unobtrusive.
- [ ] Activity renders follows, favorites, statuses, ratings and reviews correctly.
- [ ] All current UI copy is English and concise.

## Next implementation slice

1. Notifications.
2. Confirm and implement Reviews, Lists and Journal only where real data exists.
3. Error and static pages.
4. Footer and global responsive pass.
5. Remove superseded CSS and duplicate selectors.
