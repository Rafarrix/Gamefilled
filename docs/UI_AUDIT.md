# Gamefilled V2 UI Audit

This document is the working checklist for the full visual and interaction audit. Page-family rules live in `docs/UI_SYSTEM.md`.

## Non-negotiable rules

- Every page background must merge with the global `#0e1015` canvas. Page wrappers must not create visible strips or container-sized background blocks.
- Solid bright-green buttons are reserved for clear primary actions. Secondary actions use dark surfaces with a restrained green accent.
- Product copy must be concise and action-focused. Avoid privacy, roadmap or explanatory paragraphs scattered through normal pages.
- All user-facing copy remains in English until localization is implemented.
- Desktop, ultrawide, tablet and mobile must each be reviewed before a route is marked complete.
- Empty, loading, partial-data and error states must use shared components.
- Page-specific CSS should not redefine global colors, button styles or content widths.
- A redesign is not automatically an improvement. Preserve an existing composition when it is clearer or more distinctive.
- Provider attribution belongs in global attribution areas such as the footer, not inside normal user actions or discovery copy.
- Personal routes should speak directly to the owner (`your library`, `your friends`, `you rated`) while remaining readable for visitors viewing another profile.
- Navigation must not expose empty, fake or broken destinations.

## Review states

- `Stable`: visually reviewed and functionally confirmed.
- `In audit`: currently being standardized in PR #7.
- `Pending`: still uses legacy or inconsistent presentation.
- `Hidden until real`: intentionally removed from navigation until a functional data model exists.

## Core routes

| Area | Routes | State | Notes |
| --- | --- | --- | --- |
| Home | `/` | Stable / recheck | Recheck ultrawide spacing and logged-in states. |
| Game discovery | `/games`, `/games/lib/*` | Stable / recheck | Recheck empty and service failure states. |
| Game details | `/games/{id}` | Stable / recheck | Recheck media room, universe, timeline and personal actions on mobile. |
| Companies | `/company`, `/company/{id}/{slug}` | Stable / recheck | Recheck search errors and long company names. |
| People | `/people` | Stable / recheck | Recheck filters and cards on narrow screens. |
| Profile overview | `/u/{username}` | In audit | One profile hero, one tab bar and one shared profile family. |
| Profile activity | `?tab=activity` | In audit | Renders inside the profile shell. Dedicated legacy route remains compatible. |
| Profile reviews | `?tab=reviews` | In audit | Renders inside the profile shell; dedicated route redirects to the tab. |
| Profile lists | `?tab=lists` | Hidden until real | Removed from active navigation until list storage and actions exist. |
| Social lists | followers, following, friends | In audit | Personal owner copy and shared People/Friends cards implemented. |
| Authentication | Login, Register, Logout | In audit | Established composition restored; concise copy only. |
| Settings | Overview, Profile, Favorite Games, Account | In audit | Uniform background, widths and action components. |
| Legacy settings | Blocked, Defaults, Account standing | In audit | Converted from old Bootstrap/Portuguese placeholders to honest planned states. |
| Planned settings | Notifications, Integrations | In audit | Clearly planned; no fake save controls. |
| Search | `/Search` and header autocomplete | In audit | Gamefilled catalog copy and shared result/error states implemented. |
| Notification inbox | `/notifications` | Hidden until real | Bell removed until inbox, unread state and notification storage exist. |
| Library pages | Played, Playing, Backlog, Wishlist | In audit | Real `UserGameEntries`, personal copy, sorting and pagination implemented. |
| Static content | About, Contact, Terms, Privacy | In audit | Editorial shell added; production legal review still required. |
| Errors | 403, 404, 500 | In audit | Shared Gamefilled system state and status-code re-execution implemented. |
| Journal | profile subpage | Hidden until real | Removed from navigation until a real journal model exists. |

## Current PR #7 validation

- [ ] Login desktop matches the established pre-audit composition.
- [ ] Register desktop matches the established pre-audit composition.
- [ ] Authentication works on mobile without horizontal scrolling.
- [ ] Settings background is visually continuous with the body.
- [ ] Settings scales appropriately on 1440p and ultrawide monitors.
- [ ] Favorite Games feels like part of Settings.
- [ ] Favorite Games search, drag-and-drop, remove and crown actions still work.
- [ ] Save actions use standardized button treatments.
- [ ] Email change validates current password and duplicate emails.
- [ ] Password change validates current password and confirmation.
- [ ] Followers, Following and Friends use personal copy for the profile owner.
- [ ] Profile social metrics open dedicated list pages.
- [ ] Activity and Reviews remain inside the shared profile shell.
- [ ] Played, Playing, Backlog and Wishlist counts match the profile.
- [ ] Library search, sort and pagination preserve the selected section.
- [ ] Library hero balances title, owner identity and total on ultrawide/mobile.
- [ ] Search contains no provider-facing product copy.
- [ ] Footer stays on one row on desktop and attribution remains unobtrusive.
- [ ] Autocomplete supports long titles, keyboard use and two-axis pointer glow.
- [ ] About, Contact, Terms and Privacy open without visual background islands.
- [ ] Invalid routes render the Gamefilled 404 state.
- [ ] No visible Lists, Journal or Notifications links lead to unfinished pages.
- [ ] All current UI copy is English and concise.

## Remaining audit slices

1. Runtime review of the shared profile family on desktop, ultrawide and mobile.
2. Home, discovery and entity-family responsive recheck.
3. Implement real Notifications only after schema and event rules are defined.
4. Design and implement Lists and Journal as separate product features.
5. Remove superseded CSS and duplicated legacy selectors after visual confirmation.
6. Final accessibility, security, performance and production-readiness passes.
