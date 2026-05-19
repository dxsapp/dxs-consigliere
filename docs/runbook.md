# Consigliere operator runbook

> **Status:** wave-A3 in progress. The TLS section below is
> the only finalised section; S7 will finish the runbook
> (deployment / DR / secret rotation / on-call).

## TLS termination (wave-A3 S0)

Consigliere ships behind a Caddy 2 reverse proxy. Caddy
owns ACME / Let's Encrypt issuance + renewal + the HSTS
header; the .NET application never touches certificate
material.

### Topology

```
[ public DNS ] ──▶ [ Caddy :80/:443 ] ──▶ [ Kestrel :5000 ] ──▶ [ Raven :8080 ]
                       (TLS terminates here)        (internal docker network only)
```

Two compose profiles:

- `dev`: self-signed via `tls internal`. Use this for local
  smoke + Playwright e2e against the prod-shaped stack.
- `prod`: Let's Encrypt with the operator's public DNS.

### Dev profile

```sh
docker compose --profile dev up --build
```

- Caddy picks up `infrastructure/caddy/Caddyfile.dev` and
  serves its own self-signed CA on :443.
- Visit `https://localhost/setup` and accept the browser
  warning, or import Caddy's root CA from
  `docker exec consigliere-caddy-dev cat /data/caddy/pki/authorities/local/root.crt`
  to silence the warning permanently.

### Prod profile

The operator must set two env vars before `docker compose
--profile prod up`:

```sh
export CADDY_DOMAIN="consigliere.example.com"
export CADDY_EMAIL="ops@example.com"
```

Pre-flight checks:

1. **DNS first.** The A/AAAA record for `$CADDY_DOMAIN`
   must already resolve to this host. ACME HTTP-01 fails
   otherwise and Caddy falls back to its self-signed CA —
   any client that hits the broken cert will pin HSTS to
   the broken issuer for a year.
2. **Open inbound :80 + :443.** Port 80 is needed for the
   HTTP-01 challenge.
3. **Persist the data volume.** Cert + ACME state live on
   the `caddy-data` named volume. Backups MUST include it
   or every restart re-hits Let's Encrypt rate limits.

Bring it up:

```sh
docker compose --profile prod up -d --build
curl -I https://${CADDY_DOMAIN}/
# Expected: 200 OK, strict-transport-security header set.
```

### Cookie Secure flag

Cookie `SecurePolicy` binds from `Consigliere:AdminAuth:
cookieSecure` (env var:
`Consigliere__AdminAuth__cookieSecure`).

| Environment              | Value           | Why                                   |
|--------------------------|-----------------|---------------------------------------|
| Production               | `Always`        | Cookie refuses to set over plain HTTP |
| DockerComposeE2E (dev)   | `SameAsRequest` | dev profile uses self-signed TLS      |
| Test (contract suite)    | `SameAsRequest` | host loops over plain HTTP            |

Parser is conservative: any unknown / null value maps to
`Always`. A misconfigured env var cannot silently drop the
Secure flag.

### X-Forwarded-* plumbing

Caddy forwards the original scheme + client IP via
`X-Forwarded-Proto` + `X-Forwarded-For`. Kestrel honours
both via `app.UseForwardedHeaders()` (wired in
`Startup.Configure`).

Without this:
- the cookie `SecurePolicy=Always` would refuse the cookie
  (Request.Scheme would be `http` from the proxy hop)
- the rate limiter (S1) would key every request on Caddy's
  loopback IP

For deployments behind a known upstream LB (e.g. Cloudflare
in front of Caddy), tighten the trust list:

```sh
export ForwardedHeadersOptions__KnownProxies__0=203.0.113.42
```

### Smoke after bring-up

```sh
# 1. HSTS header present
curl -kI https://localhost/ | grep -i strict-transport-security

# 2. Setup wizard reachable through Caddy
curl -ksf https://localhost/api/setup/status

# 3. Cookie `Secure` flag after login (dev: replace `localhost`)
curl -kc /tmp/jar -d '{"username":"admin","password":"..."}' \
  -H 'Content-Type: application/json' \
  https://localhost/api/admin/auth/login
grep '#HttpOnly_' /tmp/jar     # cookie persisted
grep 'TRUE' /tmp/jar           # Secure column = TRUE
```
