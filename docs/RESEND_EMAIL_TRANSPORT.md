# Gamefilled — Resend transactional email transport

Status: implementation is **transport only**. No public mail endpoint, account
confirmation workflow, reset token or new database exists in this PR.

## Proven connectivity

On 2026-10-20, the local Debian web-01 VM called `POST
https://api.resend.com/emails` with sender
`Gamefilled <no-reply@gamefilled.pt>` and received HTTP 200.
The verified provider domain is `gamefilled.pt`. This demonstrates that
Resend accepted a send request, not that an inbox received a message.

## Security requirements

- Provider key must be read from an absolute, runtime-mounted file whose
  contents start `re_`. Do **not** add the key to Git, images, `appsettings`,
  Docker image build args, tests, chat or application logs.
- Configure `Resend__ApiKeyFile=/run/secrets/resend_api_key` on the web
  service only when the Identity email workflows are ready; mount the
  corresponding host file **read-only**, readable by the non-root container
  account. Do not assume Compose grants access to a root-only secret file.
  Check file ownership and permissions carefully and avoid printing it.
- Do not commit changes directly to the live `/opt/gamefilled/compose.yaml`
  without review. Keep the old image/database and a rollback path.
- The sender uses a fixed verified identity
  `Gamefilled <no-reply@gamefilled.pt>`, limits recipients to a single
  properly formatted address, and supplies a user agent to avoid the
  Python urllib/Cloudflare 403 seen in the original connectivity test.
- Caller must construct confirmation/reset URLs from a fixed, allowlisted
  HTTPS application origin. Do not derive action links from HTTP request
  Host headers, and do not log raw reset tokens.
- Disable provider click tracking and open tracking for authentication emails
  in the Resend dashboard. Do not insert analytics scripts or tracking pixels.
- Limit sends/re-sends per IP and account/email, hide account existence,
  monitor quota and error rates, and never send a raw password.
- Failed/missing key results in an exception, not a fallback test delivery,
  logged key or a confirmation message pretending email was sent.
- Test the email provider in staging, including 401/403, 429, 5xx and
  recipient delivery, before publicly enabling the feature.

## Integration sequence

1. Complete a fresh Identity model and EF Core database on isolated staging.
2. Add `ITransactionalEmailSender` injection to identity confirmation and
   reset workflows; make verification mandatory for new accounts.
3. Provision and mount the secret file in the deployment environment.
4. Exercise confirmation, expiration, single-use resets, throttling and
   session invalidation in database-backed integration tests.
5. Stage and approve before an explicit cutover; do not expose the app on WAN.

No Resend key is required to build this PR, run its tests or host the
existing Gamefilled application because the transport has no active call path.
