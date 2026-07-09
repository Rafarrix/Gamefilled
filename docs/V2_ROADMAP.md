# Gamefilled V2 Roadmap

## Goal

Evolve the PAP version into a production-ready gaming discovery and social platform without discarding the existing ASP.NET Core backend or the current dark and green Gamefilled identity.

## Branch strategy

- `master`: stable PAP baseline.
- `v2-development`: integration branch for Gamefilled V2.
- `feature/*`: isolated development merged through Pull Requests.

## Initial technical audit

### Existing strengths

- ASP.NET Core Razor Pages with Entity Framework Core and SQL Server.
- Existing IGDB integration and typed HTTP clients.
- User profiles, follows, activity and favourite games.
- Pagination and multiple game-library sorting modes.

### Priority problems

1. Legacy pages still contain duplicated IGDB access logic.
2. Authentication is custom and session-based.
3. Login throttling is tied to the visitor session.
4. Authentication responses can expose whether an account exists.
5. Registration validation needs strengthening.
6. The original database export exposed seeded data and machine-specific paths.
7. Automated tests and EF Core migrations are still missing.
8. Some interfaces are too visually close to Backloggd and need a clearer Gamefilled identity.

## Phase 0 — Foundation

- [x] Create `v2-development`.
- [x] Create safe database schema and configuration examples.
- [x] Add GitHub Actions restore/build validation.
- [ ] Add a complete README with local setup instructions.
- [ ] Introduce EF Core migrations.
- [ ] Add unit and integration test projects.

## Phase 1 — Discovery engine

- [x] Create `IGameDiscoveryService` and implementation.
- [x] Add typed pagination, sorting and filter models.
- [x] Add search, platform, genre, year, rating and release-state filters.
- [x] Preserve filter state through URLs and pagination.
- [x] Cache platforms and genres.
- [x] Add an accessible off-canvas filter drawer.
- [x] Use IGDB PopScore Visits for Trending with a safe fallback.
- [ ] Migrate average play-time and finish-time sorting into the V2 service.
- [ ] Add automated query-builder tests.

## Phase 2 — Companies

- [x] Add canonical `/company/{id}/{slug}` pages.
- [x] Link companies from game pages.
- [x] Distinguish developer, publisher, porting and supporting roles.
- [x] Show description, logo, founding date, status, size, parent and official websites.
- [x] Build a chronological associated-games catalogue.
- [ ] Add company search and directory pages.
- [ ] Add subsidiaries and a visual company network.

## Phase 3 — Game details and media

- [x] Use the shared IGDB client for game details.
- [x] Add clickable involved companies.
- [x] Add trailers when the IGDB supplies a valid YouTube ID.
- [x] Preserve artworks, screenshots, ratings, follows, hypes and time-to-beat.
- [ ] Add similar games, franchises and collections.
- [ ] Add DLCs, expansions, ports, remakes and remasters.
- [ ] Add release timelines by platform and region.
- [ ] Add engines, multiplayer modes, perspectives and language support.
- [ ] Add official store/platform links and age-rating information.

## Phase 4 — Distinct Gamefilled design system

- [ ] Define design tokens for spacing, typography, surfaces, radii and interaction states.
- [ ] Replace remaining Backloggd-like layouts with a distinctive Gamefilled system.
- [ ] Prefer editorial, asymmetrical and data-storytelling layouts.
- [ ] Create reusable cards, drawers, media blocks and metadata components.
- [ ] Redesign the homepage, game page and user profile around the new system.
- [ ] Complete keyboard, contrast and reduced-motion reviews.

## Phase 5 — Authentication and security

- [ ] Migrate custom users to ASP.NET Core Identity.
- [ ] Add email confirmation and password recovery.
- [ ] Replace session throttling with server-side rate limiting.
- [ ] Use generic login errors.
- [ ] Review authorization, CSRF, XSS, cookies, headers and uploads.
- [ ] Complete an OWASP-oriented pre-production review.

## Phase 6 — Database, tests and operations

- [ ] Add EF Core migrations.
- [ ] Add unit tests for query normalization and generation.
- [ ] Add integration tests for company and game services.
- [ ] Add route smoke tests.
- [ ] Add structured logging and health checks.

## Phase 7 — Docker and production

- [ ] Add a multi-stage Dockerfile.
- [ ] Add a development Docker Compose environment.
- [ ] Move production configuration to environment variables and secrets.
- [ ] Define deployment, HTTPS, monitoring, backups and recovery.
- [ ] Prepare privacy policy and terms before public registration.
