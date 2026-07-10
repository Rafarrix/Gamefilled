# Gamefilled V2 UI System

This document is the implementation contract for the visual audit. Pages may be distinctive, but they must share the same foundations.

## Core visual language

- Canvas: `#0e1015`.
- Primary surface: `#171b23`.
- Raised/hover surface: `#1b2029`.
- Default border: `rgba(255,255,255,.08)`.
- Green is an accent and action state, not a page background.
- Interactive elements use 9–14 px radii; large editorial panels may use 17–22 px.
- Body copy stays below 75 characters per line where practical.
- Every interactive card needs hover, keyboard focus and a useful destination.
- Buttons use one of two shared treatments: accent action or dark secondary action.
- Empty, loading and error states must explain the next useful action.

## Page families

### 1. Discovery
Routes: `/`, `/games`, `/Search`, `/company`, `/people`.

Shared structure:
- clear discovery heading;
- search/filter controls;
- responsive result grid;
- useful empty/error states;
- no provider branding inside the user flow.

Distinctive behavior:
- Games may use dense cover grids;
- Companies use editorial identity cards;
- People use social identity and relationship states.

### 2. Entity
Routes: `/games/{id}`, `/company/{id}/{slug}`.

Shared structure:
- editorial hero;
- identity metadata;
- actions near the identity;
- grouped information instead of equal-weight boxes;
- media and related content separated from the footer.

Distinctive behavior:
- Game owns Game Universe, Media Room, release timeline and personal tracking;
- Company owns company history, links and catalogue.

### 3. Profile
Routes: `/u/{username}` plus the profile tabs.

Shared structure:
- one hero and one tab bar;
- Overview, Activity, Reviews and future Lists render inside the profile shell;
- large collections such as Followers, Following, Friends and Library may use dedicated routes, but reuse the same identity and navigation language;
- copy speaks directly to the owner (`Your activity`) and descriptively for visitors (`Rafael's activity`).

### 4. Social lists
Routes: `/people`, `/u/{username}/followers`, `/following`, `/friends`.

Shared structure:
- reusable person card;
- avatar, display name, username, presence, bio and relationship state;
- follow actions only where they add value;
- consistent mutual/friend definition.

### 5. Library
Routes: `/u/{username}/games`, `/playing`, `/backlog`, `/wishlist`.

Shared structure:
- library identity and count;
- four-state navigation;
- search, sort and pagination;
- cover card with rating/review signal;
- owner card only when viewing someone else's library.

### 6. Settings
Routes under `/Settings`.

Shared structure:
- one sidebar;
- one settings shell;
- functional settings are clearly distinguished from planned settings;
- save/cancel actions remain close to the changed content;
- no isolated background islands.

### 7. Authentication
Routes: `/users/Login`, `/users/Register`.

Shared structure:
- preserve the established large-logo artwork composition;
- form remains concise;
- responsive improvements must not reduce the logo to an icon;
- no roadmap/privacy marketing copy inside the form.

### 8. Static and system
Routes: About, Contact, Terms, Privacy, Error and not-found states.

Shared structure:
- narrow readable content column;
- shared heading hierarchy;
- consistent footer separation;
- error pages provide a way back to discovery.

## Responsive acceptance

Every audited route must be checked at:
- 360–430 px mobile;
- 768–1024 px tablet;
- 1280–1920 px desktop;
- 2560 px and wider ultrawide.

No horizontal overflow, clipped drawers, detached footer, oversized empty areas or unreadably stretched text.

## Interaction acceptance

- Keyboard focus is visible.
- Hover effects do not move layout.
- Drawers and dropdowns close with Escape when applicable.
- Spoilers require deliberate reveal.
- Tabs and filters preserve useful URL state.
- Provider attribution stays in the footer or technical documentation.
