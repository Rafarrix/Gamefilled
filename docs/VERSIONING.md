# Gamefilled — versioning convention

The version labels are deliberately product-history labels, not semantic
version major numbers.

## V1 — PAP (completed)

**V1 means only the Gamefilled version shown at the original school PAP
presentation (June 2026).** It is a historical snapshot and should remain
traceable, read-only and unambiguously labelled. Do not retroactively label
changes after the PAP as V1.

A precise archival Git tag requires identifying and verifying the exact
presentation commit; do not arbitrarily tag today's master as V1.

## V2 — all post-PAP development

**Every improvement made after the PAP belongs to Gamefilled V2**, including:

- Current privately hosted prototype at `dev.gamefilled.pt` on `web-01`.
- HTTPS, Docker, Caddy, Proxmox, SQL Server migrations, CI and hardening.
- Complete backend modernization to ASP.NET Core Identity.
- An isolated new SQL database for development, clean authorization
  boundaries, email confirmation and password recovery through Resend.
- Catalog, user libraries, community, moderation, UX polishing and launch
  work.

The folder `v2-modernization/` is a **parallel development workspace within
V2**. It is not V3 and is **not** a deployed second public site. The current
V2 remains stable while the new backend is tested.

Internal release identifiers such as `v2-dev`, `v2-auth-preview`,
`v2-rc1` and `v2.0.0` may be used later to identify builds. They do
not redefine the PAP project as a deployed V2.

## Data safety

Accounts and saved games in the present environment are disposable test data,
but never delete them unexpectedly. The current V2 database stays intact until
a separate V2-modernization database and all critical features pass testing.
Keep an off-host backup and a confirmed restore path before cutover.

## Repository conventions

- Public-facing title: **Gamefilled V2** for all post-PAP work.
- Historical presentation archive: **Gamefilled V1 (PAP)**.
- Technical foundation: `v2-modernization/Gamefilled.V2`.
- Test project: `tests/Gamefilled.V2.Tests`.
- Related docs: `docs/V2_BACKEND_MODERNIZATION.md` and
  `docs/V2_IDENTITY_FOUNDATION.md`.
- No 'V3' branch, runtime hostname, release promise or public branding.
