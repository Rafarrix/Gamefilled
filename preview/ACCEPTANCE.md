# Gamefilled V2 — temporary backend-lab acceptance (NOT product UI acceptance)

**Important (2026-10-22):** This runs a separate partial Razor Pages app and does NOT
contain the complete Gamefilled UI. A passing checklist here does **not**
mean V2 feature parity or acceptance for a real product preview; compare
against `Gamefilled.csproj` with the mandatory requirements in
`docs/V2_PRODUCT_INTEGRATION.md`. Do not approve launch or production
cutover using only this worksheet.

This is a **test-session worksheet**. Run only after the isolated preview
is up and both `/health/live` and `/health/ready` report success. Use
synthetic accounts and game data; do not import the running V2 database.

## 1. Security and basic runtime

- [ ] From a LAN browser, `https://preview.gamefilled.pt:8444` uses the
      preview Caddy certificate and no public WAN port forwarding.
- [ ] The preview homepage shows the Git commit deployed, matching
      `bash preview/previewctl.sh status`.
- [ ] `bash preview/previewctl.sh smoke` verifies trusted TLS, landing page,
      both health routes, game search and anonymous profile access denial.
- [ ] Cookies use `Secure`, `HttpOnly` and `SameSite=Lax`.
- [ ] Browser checks: desktop, mobile width and keyboard tab navigation.
- [ ] No application SQL port or Kestrel backend port is exposed on LAN.
- [ ] `/health/ready` returns non-200 if preview SQL is unavailable.
- [ ] Authentication and confirmation still work after a **preview-only**
      web container restart (persistent Data Protection keys).
- [ ] The currently running Gamefilled site (e.g. `gamefilled.home.arpa`
      or its existing private alias) behaves exactly as before.
- [ ] The preview SQL volume and container names are distinct from those
      of the current `Gamefilleddb`.

## 2. Accounts and session flows

- [ ] Register an account with a controlled test email.
- [ ] Account cannot log in until Resend verification completes.
- [ ] Click confirmation link and finish confirmation via explicit POST.
- [ ] Token cannot be reused and expiry is handled gracefully.
- [ ] Login, logout and forgot/reset password function correctly.
- [ ] Old password no longer works; old session revalidates according
      to configured security-stamp period.
- [ ] No error reveals whether a guessed email exists.

## 3. Catalog and game detail (without query calibration changes)

- [ ] Search several known games by title; compare ranking with the
      currently running V2, report discrepancies for human relevance review.
- [ ] Search empty, ambiguous and misspelled titles; capture intended vs
      actual results without changing API criteria during the session.
- [ ] Open a game page, validate cover, title, genres and platforms.
- [ ] Validate screenshots, trailers and release timeline, if available.
- [ ] Check DLC, remasters, expansions and recommended similar games
      against expected game relationships.
- [ ] A missing ID gives a suitable 404, IGDB timeout does not corrupt
      the account's saved library.
- [ ] Compare screenshot/layout of the preview game page with the
      existing V2; keep the existing design patterns.

## 4. Identity-owned library

- [ ] Add a game from its detail page, choose Played/Playing/Backlog/Wishlist.
- [ ] Library presents the right cover and title, with pagination.
- [ ] Rate from 1 to 10, write review, mark spoiler, edit and remove.
- [ ] Changing status does not erase an existing review/rating.
- [ ] Another authenticated test account cannot see private library data
      or edit the first account's game, even using crafted requests.
- [ ] Anonymous POST and POST without CSRF fail closed.

## 5. Private profile (PR #40)

- [ ] The authenticated viewer opens `/profile` and sees their own username,
      display name, join month, and real library counts.
- [ ] Editing a display name updates only the viewer's account and safely
      displays special characters as text; no raw HTML executes.
- [ ] A second account has independent counters and display name.
- [ ] Anonymous visitors cannot open the private profile and a POST without
      a CSRF token is rejected.

## 6. Not yet part of this preview's acceptance

Public profiles, friend/follow graph, notifications, community feed,
Steam account linking,
personalized FY homepage, moderation, GDPR export, final UI parity and
WAN/public launch. They remain tracked as separate roadmap items.

**Outcome per case:** Pass / Fails / Needs visual polish / Needs relevance
calibration / Not implemented. Record which game IDs and the expected
result, but never share API keys, passwords, private links or cookies.
