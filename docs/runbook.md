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

Bring it up via the `compose.prod.yml` override (the
`caddy-prod` service lives there, isolated from the dev
interpolation pass — S0-audit M1 fix):

```sh
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod up -d --build
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
in front of Caddy), tighten the trust list at code level —
`ForwardedHeadersOptions.KnownProxies` is `IList<IPAddress>`,
and the .NET configuration binder does not round-trip
`IPAddress` from env vars. Hard-code the upstream IPs in
`ProxyHeadersSetup.AddConsigliereForwardedHeaders` if the
default loopback-trust is too permissive for the deployment.

## Health probes (wave-A3 S2)

Three anonymous endpoints designed for k8s / Railway / any
orchestrator that ships HTTP probes:

| Path              | Tag       | Pass when…                                    | Failure code |
|-------------------|-----------|-----------------------------------------------|--------------|
| `/health/live`    | (none)    | the process is listening                      | n/a (always 200) |
| `/health/ready`   | `ready`   | Raven + at least one configured provider up   | 503 with `degraded`/`unhealthy` body |
| `/health/startup` | `startup` | the DI graph resolved without throwing        | 503 |

JSON shape:

```json
{
  "status": "healthy" | "degraded" | "unhealthy",
  "checks": [
    { "name": "raven", "status": "healthy",
      "description": null, "durationMs": 12 }
  ]
}
```

`description` is intentionally terse — these endpoints are
anonymous, so no hostnames / stack traces / status codes
leave the process.

### Sample k8s probe block

```yaml
livenessProbe:
  httpGet: { path: /health/live, port: 5000 }
  periodSeconds: 10
readinessProbe:
  httpGet: { path: /health/ready, port: 5000 }
  periodSeconds: 5
startupProbe:
  httpGet: { path: /health/startup, port: 5000 }
  failureThreshold: 30
  periodSeconds: 2
```

Caddy `prod` profile bypasses the access log for
`/health/*` (one matcher in `Caddyfile.prod`) so a 5-second
probe cadence doesn't drown out real traffic. The wave-A3
S1 rate limiter will use the same matcher to exempt probes
from the auth bucket.

## Rate limiting (wave-A3 S1)

Three named policies attached to the load-bearing endpoints
via `[EnableRateLimiting(...)]`:

| Policy      | Endpoint                          | Default          | Threat model        |
|-------------|-----------------------------------|------------------|---------------------|
| `login`     | `POST /api/admin/auth/login`      | 5 / minute / IP  | brute-force         |
| `me`        | `GET /api/admin/auth/me`          | 100 / minute / IP| client poll storm   |
| `broadcast` | `POST /api/tx/broadcast`          | 1 / second / IP  | runaway client      |

Health endpoints are explicitly opted out via
`.DisableRateLimiting()` — orchestrator probes hammer them on
purpose.

### Overrides

Each policy binds from `RateLimiting:*` config; env-var
form:

```sh
# Loosen login to 10/minute on a high-traffic deployment
export RateLimiting__Login__PermitsPerMinute=10
# Raise the broadcast ceiling to 5/sec
export RateLimiting__Broadcast__PermitsPerSecond=5
```

Process restart picks up the change. The S1 limiter is
per-process; multi-instance HA is wave-A4 territory.

### Client behaviour

The admin UI's `ApiClient` honours `Retry-After` exactly
ONCE: it sleeps the indicated delta, retries, and if the
second response is also 429 surfaces an
`AppError { category: "RateLimited" }`. The header is
clamped to 65 seconds so a buggy / malicious server cannot
freeze the UI for hours.

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
