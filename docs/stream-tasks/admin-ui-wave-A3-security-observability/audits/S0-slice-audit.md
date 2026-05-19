# wave-A3 S0 slice audit

MAJOR REVISION REQUIRED

Audit range checked: `b386725..ade41b0` on `codex/consigliere-vnext`.

Note: the prompt and `master.md` Delivery Notes name `8ba09a5`, but current branch HEAD is `ade41b0`. The two commits differ only in the Delivery Notes row; the deliverable itself was audited from the current branch.

## Findings

### M1 — Dev profile cannot be parsed without prod env vars

- file: `compose.yml:65`
- why it matters: `docker compose --profile dev config` fails before Compose applies profiles because `${CADDY_DOMAIN:?...}` and `${CADDY_EMAIL:?...}` are interpolated for the inactive `caddy-prod` service. This breaks the S0 validation command and makes the dev profile unexpectedly depend on production ACME variables.
- recommended fix: remove required-variable interpolation from the profiled service and enforce prod-only validation at prod container startup instead. For example, set `CADDY_DOMAIN: "${CADDY_DOMAIN:-}"` / `CADDY_EMAIL: "${CADDY_EMAIL:-}"`, then add a `command: sh -c 'test -n "$$CADDY_DOMAIN" && test -n "$$CADDY_EMAIL" || exit 1; exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile'` on `caddy-prod`.

Evidence:

```sh
docker compose --profile dev config
# parsing compose.yml: error while interpolating services.caddy-prod.environment.CADDY_DOMAIN:
# required variable CADDY_DOMAIN is missing a value
```

### M2 — Dev Caddy HTTPS endpoint fails TLS handshake

- file: `infrastructure/caddy/Caddyfile.dev:29`
- why it matters: the S0 dev profile is supposed to provide usable `tls internal` HTTPS and make `curl -kI https://localhost/` return HSTS. The container starts, but `https://localhost/` and `https://127.0.0.1/` both fail during TLS handshake, so dev TLS cannot be used to validate Secure cookies or ForwardedHeaders behavior.
- recommended fix: give the dev TLS site explicit local subjects instead of a hostless `:443` site, for example `localhost:443, 127.0.0.1:443 { tls internal ... }`, and keep the `:80` redirect block. Re-run `curl -kI https://localhost/` and verify the HSTS header.

Evidence:

```sh
CADDY_DOMAIN=example.com CADDY_EMAIL=ops@example.com docker compose --profile dev up -d --build
curl -kI --max-time 10 https://localhost/
# curl: (35) LibreSSL/3.3.6: error:1404B438:SSL routines:ST_CONNECT:tlsv1 alert internal error

curl -I --max-time 10 http://localhost/
# HTTP/1.1 301 Moved Permanently
# Location: https://localhost/
```

### L1 — Delivery Notes records the wrong S0 commit

- file: `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md:242`
- why it matters: the ledger points auditors to `8ba09a5`, but the actual current branch S0 commit is `ade41b0`. `8ba09a5` is an alternate commit whose Delivery Notes row is still `_pending hash_`, so the recorded range is ambiguous.
- recommended fix: after the audit fold lands, update the S0 Delivery Notes row to the final committed S0/audit-fold commit hash.

### L2 — Runbook hardening command is not wired to options binding

- file: `docs/runbook.md:103`
- why it matters: the runbook tells operators to tighten forwarded-header trust with `ForwardedHeadersOptions__KnownProxies__0`, but `AddConsigliereForwardedHeaders()` does not bind `ForwardedHeadersOptions` from configuration. Operators would think they narrowed trust while the application still clears `KnownNetworks` and `KnownProxies`.
- recommended fix: either remove the hardening command until S7, or change `AddConsigliereForwardedHeaders` to accept `IConfiguration` and bind a documented `ForwardedHeaders` config section after setting the defaults.

## Positive Checks

- No new public TLS/certificate-store code was found under `src/Dxs.Consigliere`. The only `X509Certificate2` usage is the existing RavenDB client certificate path in `RavenDbDocumentStore.cs`.
- RavenDB moved from host `ports` to `expose: 8080`; Consigliere exposes `5000` only inside the compose network.
- With `CADDY_DOMAIN` and `CADDY_EMAIL` set, profiles are mutually exclusive:
  - `docker compose --profile dev config --services` => `ravendb`, `consigliere`, `caddy-dev`
  - `docker compose --profile prod config --services` => `ravendb`, `consigliere`, `caddy-prod`
- `Caddyfile.prod` uses `{$CADDY_DOMAIN}` plus global `email {$CADDY_EMAIL}` and has no explicit internal TLS fallback path.
- Cookie SecurePolicy parser defaults unknown/null/empty values to `CookieSecurePolicy.Always`.
- `app.UseForwardedHeaders()` runs before `UseCors()` and `UseRouting()`.

## Validation Evidence

- `dotnet build -c Release` passed with existing warnings, 0 errors.
- `pnpm verify` passed.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract` passed 16/16.
- `dotnet test --filter "Setup.ProxyHeadersSetupTests|Setup.AdminAuthSetupCookieSecureTests"` passed 13/13.
- `docker compose --profile dev config` failed due M1.
- Manual `curl -kI https://localhost/` after dev profile bring-up failed due M2.
