# Gamefilled V2

## Goal

Evolve the PAP version into a production-ready gaming discovery and social platform without discarding the existing ASP.NET Core backend or the current visual identity.

## Branch strategy

- `master`: stable PAP baseline.
- `v2-development`: integration branch for Gamefilled V2.
- `feature/*`: isolated work such as discovery filters, trailers, design system and authentication.

## Initial technical audit

### Existing strengths

- ASP.NET Core Razor Pages with Entity Framework Core and SQL Server.
- IGDB integration already separated into `IgdbClient` and `IgdbTokenProvider`.
- Typed `HttpClient` for IGDB and an in-memory OAuth token cache.
- Existing user profiles, follows, activity and favorite games.
- Pagination and several library sorting modes already implemented.

### Priority problems

1. `_GamesLibBase` implements a second IGDB client, including token retrieval, HTTP calls, parsing and error handling. This duplicates `IgdbClient` and should be replaced by a single service layer.
2. Authentication is custom and based on session values. It should eventually migrate to ASP.NET Core Identity before production.
3. Login throttling is stored in the visitor session and can be bypassed by clearing cookies or starting a new session.
4. Login responses distinguish between an unknown user and an incorrect password, enabling account enumeration.
5. Registration trims passwords, accepts a minimum of only six characters and performs very basic email validation.
6. The original database export contained seeded emails and password hashes and used machine-specific SQL Server file paths.
7. There are no automated tests, migrations, health checks or CI workflow yet.
8. The repository did not include a safe configuration example or a setup guide.

## V2 delivery phases

### Phase 0 — Foundation

- [x] Create `v2-development` branch.
- [x] Replace the database dump with a portable schema-only script in the V2 branch.
- [x] Add `appsettings.example.json`.
- [ ] Add a complete README with local setup instructions.
- [ ] Introduce EF Core migrations and stop treating a generated SQL dump as the source of truth.
- [ ] Add a basic test project.
- [ ] Add GitHub Actions build and test workflow.

### Phase 1 — Discovery architecture

- [ ] Create a `GameDiscoveryService` interface and implementation.
- [ ] Move all IGDB requests from `_GamesLibBase` into the IGDB infrastructure layer.
- [ ] Introduce strongly typed filter and pagination request models.
- [ ] Add platform, genre, release-year and rating filters.
- [ ] Keep filters and sorting in the URL query string.
- [ ] Add metadata caching for platforms and genres.
- [ ] Add graceful timeout, cancellation and retry handling.

### Phase 2 — Game details and media

- [ ] Add IGDB videos to game detail DTOs.
- [ ] Render YouTube trailers only when a valid video is available.
- [ ] Improve screenshots, artworks, companies and platform presentation.
- [ ] Add similar games and related content.
- [ ] Add proper empty, loading and error states.

### Phase 3 — Frontend redesign

- [ ] Define CSS design tokens for colors, spacing, typography, radii and shadows.
- [ ] Build reusable game-card, filter, button, input and modal components.
- [ ] Redesign homepage and discovery pages while preserving the Gamefilled colors and identity.
- [ ] Implement a responsive mobile filter drawer.
- [ ] Improve accessibility, keyboard navigation and contrast.
- [ ] Optimize images, lazy loading and layout stability.

### Phase 4 — Authentication and security

- [ ] Migrate custom users to ASP.NET Core Identity.
- [ ] Add email confirmation and password recovery.
- [ ] Replace session-based login throttling with server-side rate limiting.
- [ ] Use generic authentication failure messages.
- [ ] Validate and normalize usernames and emails consistently.
- [ ] Review CSRF, XSS, authorization, cookies, headers and uploads.
- [ ] Perform an OWASP-based pre-production review.

### Phase 5 — Docker and production

- [ ] Add application Dockerfile.
- [ ] Add Docker Compose for the app and SQL Server development environment.
- [ ] Add environment-based configuration and secret management.
- [ ] Add health checks, structured logging and backups.
- [ ] Configure HTTPS, domain, deployment and monitoring.
- [ ] Prepare privacy policy and terms before public registrations.

## First implementation sprint

The first V2 sprint should focus on one vertical slice:

1. Refactor the popular games page to use a new centralized discovery service.
2. Add platform and genre metadata endpoints.
3. Implement platform and genre filters in the library page.
4. Preserve pagination and sorting in query parameters.
5. Add tests for filter query generation and input validation.

This establishes the architecture that all later discovery pages will reuse.
