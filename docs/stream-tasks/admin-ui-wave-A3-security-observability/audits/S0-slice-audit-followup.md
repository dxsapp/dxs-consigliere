---
created: 2026-05-19
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S0-slice-audit-prompt.md
status: applied
---

# wave-A3 S0 slice-audit followup (MAJOR REVISION REQUIRED)

Codex slice-audit on `ade41b0`: MAJOR REVISION REQUIRED
(0C / 0H / 2M / 2L). All 4 findings folded in this commit.

## M1 — `docker compose --profile dev config` failed on prod env interpolation

**Verified:** Docker Compose interpolates every service's
`environment` block during config-parse — *before* applying
`profiles`. The `caddy-prod` service required `CADDY_DOMAIN`
+ `CADDY_EMAIL` via `${VAR:?...}`, so any dev-profile
invocation that didn't set those vars failed at parse time
with `required variable CADDY_DOMAIN is missing a value`.

**Revision applied:**

- Extracted `caddy-prod` from `compose.yml` into a new
  `compose.prod.yml` override file. The mandatory env-var
  enforcement stays there; it only triggers when the
  operator opts in with `-f compose.prod.yml`.
- `compose.yml` now contains only the shared services + the
  `caddy-dev` profile.
- Updated `docs/runbook.md` to show the prod invocation:
  `docker compose -f compose.yml -f compose.prod.yml --profile prod up`.
- Manually re-verified:
  - `docker compose --profile dev config` — parses 0/0.
  - `CADDY_DOMAIN=x CADDY_EMAIL=y docker compose -f compose.yml -f compose.prod.yml --profile prod config` — parses, materialises `caddy-prod`.

## M2 — `https://localhost/` TLS handshake failed under dev profile

**Verified:** the dev Caddyfile bound `:443 { tls internal }`
— a bare port without an SNI value. Caddy's internal CA
needs a hostname to put in the cert's SAN, so the listener
accepted TCP but had no matching cert and the TLS handshake
aborted. The HSTS smoke (`curl -kI https://localhost/`)
returned no response.

**Revision applied:**

- `Caddyfile.dev` now declares concrete site addresses:
  `https://localhost, https://127.0.0.1 { tls internal ... }`.
  `localhost` covers browser-side dev; `127.0.0.1` covers
  `curl`'s default resolution and the Playwright loop.
- The HTTP→HTTPS redirect block is now scoped to
  `http://localhost` so it doesn't shadow ACME or other
  hostnames.
- Re-validated locally: `docker compose --profile dev up
  -d --build` + `curl -kI https://localhost/` returns
  `HTTP/2 200`, `strict-transport-security: max-age=
  31536000; includeSubDomains`, plus the five other
  security headers from `snippets/headers.caddy`. Caddy log
  shows `certificate obtained successfully` for both
  identifiers under issuer `local`.

## L1 — Ledger hash referenced `8ba09a5`; actual was `ade41b0`

**Verified:** the S0 commit was made at `8ba09a5`, master.md
was edited to record that hash, and then `git commit
--amend --no-edit` rewrote the commit to `ade41b0` —
leaving the ledger one revision behind. Hash recorded but
not pointing at the actual commit content.

**Revision applied:**

- Ledger row updated to `ade41b0`.
- New row added for this fold (`S0 audit fold`) so the next
  amend doesn't repeat the chase. The hash placeholder will
  be backfilled in a follow-up commit (without amending
  this one) once it lands.
- Personal note for the rest of wave-A3: record the
  Delivery Notes hash in a *second* commit after the
  primary slice commit lands. Amending to backfill is
  what created the L1 drift; a tiny "docs: backfill S<n>
  hash" commit costs nothing and never desyncs.

## L2 — Runbook recommended an env var that doesn't bind

**Verified:** `docs/runbook.md` suggested
`ForwardedHeadersOptions__KnownProxies__0=...` for tightening
the trust list, but `KnownProxies` is `IList<IPAddress>`
and the .NET configuration binder does NOT round-trip
`IPAddress` from `string`. Setting that env would silently
do nothing.

**Revision applied:**

- Replaced the env-var block in `docs/runbook.md` with an
  explicit note that `KnownProxies` requires *code-level*
  edits to `ProxyHeadersSetup.AddConsigliereForwardedHeaders`
  for deployments behind a known LB. Honest, no false
  affordance.

## Validation

- `dotnet build -c Release` clean (warnings unchanged).
- `docker compose --profile dev config` parses cleanly
  without any env vars set.
- `CADDY_DOMAIN=x CADDY_EMAIL=y docker compose -f
  compose.yml -f compose.prod.yml --profile prod config`
  parses; `caddy-prod` materialises.
- `docker compose --profile dev up -d --build` →
  `curl -kI https://localhost/` returns HTTP/2 200 + HSTS
  + the full security-header set.
- 13/13 S0 unit tests still pass.
- 16/16 wave-A2 contract tests still pass.

## Result

4 of 4 findings folded. The dev TLS smoke now actually
works end-to-end; the prod profile is isolated in its own
override file so it cannot brick `--profile dev` workflows.
