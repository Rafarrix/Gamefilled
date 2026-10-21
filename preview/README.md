# Gamefilled V2 — private visual preview (homelab)

**Purpose:** view and manually test the modernized post-PAP V2 in your LAN
**without replacing or touching** the current private site at
`https://dev.gamefilled.pt`.

This is **not** a public deployment and **not** the final V2 cutover.
The preview is an isolated, disposable environment for account and UI
acceptance tests. Do not expose it through MEO router port forwarding,
Cloudflare tunnel, public DNS, UPnP, or a VPN accessible to strangers.

## What is isolated

- Preview compose project: `gamefilled-v2-preview`; existing project:
  `gamefilled`. Never use the existing Compose name/path when testing.
- SQL Server 2022 Express in its own container/volume, database
  `GamefilledV2Preview`; **never** connects to `Gamefilleddb`.
- Separate .NET 10 Identity app, private Data Protection key directory and
  Resend API key file, all stored outside Git and Docker build contexts.
- Caddy terminates `https://preview.gamefilled.pt:8444` using `tls internal`.
  It binds only the chosen **LAN IP** on port `8444`. No SQL/ASP.NET
  ports are published to the host or Internet.
- The modernized frontend is still a technical preview: not all the
  community/social features and original secondary layouts exist.

## Prerequisites / manual sign-off gate

1. Confirm you are on a dedicated **copy/checkout** of this GitHub repo,
   not the live `/opt/gamefilled/src` folder or running Compose directory.
2. Check the host has spare CPU/RAM/disk for another SQL Server Express
   instance. This preview caps SQL to **3 GiB**, but it needs extra
   disk and reserves CPU while running.
3. Confirm `192.168.1.21:8444` is unused and the Docker network
   `172.28.81.0/24` does not overlap an existing network/VPN.
4. Back up the live service **off-host** and check its existing health
   before changing the homelab. Do not run `docker compose down -v`.
5. Configure the preview's API secrets privately. Do not paste them into
   chat, commit them, put them in shell command arguments or logs.
6. Obtain explicit user approval before starting preview containers on
   the physical server; preparing source and CI is safe without approval.

## Local setup, only after the manual gate

From a **new checkout** e.g. `/opt/gamefilled-preview/repo`, as an account
permitted to run Docker:

```bash
cp preview/.env.preview.example preview/.env.preview
mkdir -p preview/local/keys preview/local/secrets
# Edit preview/.env.preview LOCALLY. Choose a unique strong SQL test password,
# fill IGDB ID + secret, and set PREVIEW_LAN_IP to the web-01 LAN address.
chmod 600 preview/.env.preview
# Place the Resend key at preview/local/secrets/resend_api_key
# WITHOUT echoing it in shell history, terminals, Git or screenshots.
sudo chown -R 1654:1654 preview/local/keys
sudo chmod 700 preview/local/keys
sudo chgrp 1654 preview/local/secrets
sudo chmod 750 preview/local/secrets
sudo chgrp 1654 preview/local/secrets/resend_api_key
sudo chmod 640 preview/local/secrets/resend_api_key
docker compose --project-name gamefilled-v2-preview \
  --env-file preview/.env.preview -f compose.preview.yaml config --quiet
```

The API key should be restricted to sending for `gamefilled.pt`. The
preview sends account verification messages through the real Resend API;
use a test address you own, do not send unnecessary mail to other people.

Run `bash preview/preflight.sh` after configuring the files. Preflight
uses no network and never displays secrets. Ensure the **container** UID/GID
1654 can read the mounted secret and write its keyring; the preview
Dockerfile uses the official non-root `app` account.

## Startup (explicit manual operation — not executed by a GitHub PR)

```bash
# Run these ONLY from the dedicated preview repo checkout:
docker compose --project-name gamefilled-v2-preview \
  --env-file preview/.env.preview -f compose.preview.yaml build web migrator

docker compose --project-name gamefilled-v2-preview \
  --env-file preview/.env.preview -f compose.preview.yaml up -d sql

# The first migration attempt might fail until SQL startup finishes.
# Run it again after SQL has initialized; it applies EF migrations to
# GamefilledV2Preview ONLY. Check its isolated connection string first.
docker compose --project-name gamefilled-v2-preview --profile setup \
  --env-file preview/.env.preview -f compose.preview.yaml run --rm migrator

docker compose --project-name gamefilled-v2-preview \
  --env-file preview/.env.preview -f compose.preview.yaml up -d web caddy
```

**Testing from Windows:** add the LAN mapping `192.168.1.21
preview.gamefilled.pt` in the local `hosts` file (not public DNS).
The Caddy local CA is separate from the production CA; only install its
root certificate on your own testing machine after verifying it came from
the preview Caddy volume. For initial connectivity, curl with a supplied
CA certificate rather than disabling TLS validation.

Verify `/health/live` and `/health/ready`, then test:
signup → Resend confirmation → login → search → game details → add
to library → rate/review → logout → reset password.

If something fails, inspect **sanitized** app logs; never publish SQL
passwords, JWT/cookies, Resend API keys, tokenized URLs or full
environment dumps.

## Stopping / rollback

```bash
docker compose --project-name gamefilled-v2-preview \
  --env-file preview/.env.preview -f compose.preview.yaml stop
```

This stops preview only; the current V2 remains running as before.
Do **not** remove preview or production volumes until test data is
confirmed disposable and a backup exists. The preview can be removed
later with a separately approved cleanup plan.

## Known limitations and cutover requirements

- The database connection temporarily uses **SQL sa only in this
  disposable LAN preview**. Production requires least-privilege SQL
  runtime credentials separate from migration permissions.
- Verify that no preview port is reachable from WAN. Docker publishing
  can bypass host firewalls depending on backend/rule order; do not
  rely on a firewall rule alone.
- Persistent Data Protection keys are in a local host bind mount and
  **not encrypted by a dedicated external key protector**; suitable
  only for this isolated prototype, not final hosting.
- This does **not** migrate accounts, game data or existing volumes.
  Community and moderation are incomplete and must not be advertised
  as public-ready.
- CI validates the artifacts/builds, but **only a manual browser test
  on the homelab** establishes the actual design/interaction quality.
- Calibrate IGDB search results by evaluating relevance together, not by
  automatically changing its established filters/ranking.
