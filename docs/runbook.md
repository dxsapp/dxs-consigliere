# Consigliere — Operator Runbook

Self-contained ops handbook for Consigliere. The intended
reader is the SRE / DevOps engineer who will deploy, monitor,
and maintain a single-instance Consigliere node. No source-
code reading required.

> **Placeholder convention.** Anywhere this document uses
> angle-bracketed tokens (`<example.com>`, `<admin>`,
> `<txid>`) substitute the value appropriate for the target
> environment. Sample values are illustrative only — they are
> **not** real deployments.

## Contents

1. [First-time deployment](#1-first-time-deployment)
2. [First-run setup wizard](#2-first-run-setup-wizard)
3. [Scaling guidance](#3-scaling-guidance)
4. [Monitoring + alerting](#4-monitoring--alerting)
5. [Log streaming](#5-log-streaming)
6. [Audit log forensics](#6-audit-log-forensics)
7. [Secrets rotation](#7-secrets-rotation)
8. [Disaster recovery](#8-disaster-recovery)

---

## 1. First-time deployment

### 1.1 Prerequisites

A fresh VM running Ubuntu 22.04 LTS (or any Debian / RHEL
derivative with the same Docker package). Minimum resources
for a single-tenant deployment: **4 vCPU, 8 GB RAM, 100 GB
disk**. See §3 for sizing guidance.

Open inbound TCP **80 + 443**. No other ports are public.

A DNS A/AAAA record for `<public-domain>` must already point
at the host before you start — Let's Encrypt's HTTP-01
challenge will fail otherwise, and the resulting self-signed
fallback will pin HSTS to a broken issuer for one year. If
DNS is not ready, set the domain to localhost and use the
dev profile until cutover.

### 1.2 Install Docker + Compose

```bash
# Refresh package index + install Docker via the official repo.
sudo apt-get update
sudo apt-get install -y docker.io docker-compose-v2 git curl

# Allow the deploy user to run docker without sudo.
sudo usermod -aG docker "$USER"
newgrp docker

# Sanity check.
docker version
docker compose version
```

### 1.3 Clone the repository

```bash
git clone https://github.com/<org>/dxs-consigliere.git
cd dxs-consigliere
```

### 1.4 Configure the three production secrets

Create a `.env` file at the repo root with these three values:

```bash
cat > .env <<'EOF'
# Public domain that Caddy will obtain a Let's Encrypt cert for.
CADDY_DOMAIN=<public-domain>

# ACME contact for renewal + revocation notices.
CADDY_EMAIL=<ops@example.com>

# Bitcoin node RPC password. Read at runtime by
# Microsoft.Extensions.Configuration via the env-var key
# `BsvNodeApi__Password`.
BSV_NODE_RPC_PASSWORD=<random-32-char-hex>
EOF
chmod 600 .env
```

> **The `.env` file is the only place these values exist in
> plain text.** Treat it the same way you treat an SSH key:
> chmod 600, owned by the deploy user, never committed.
>
> **RavenDB authentication.** The bundled `ravendb` compose
> service runs in unsecured mode on the docker internal
> network (`RAVEN_Security_UnsecuredAccessAllowed:
> PrivateNetwork` — no cert, no password, reachable only from
> peer containers). The Caddy reverse proxy is the only public
> surface; nothing else publishes a port to the host. For
> deployments that require Raven cert-based auth, bind an
> external Raven cluster via `RavenDb__Urls__0` and
> `RavenDb__ClientCertificate` env vars and remove the
> bundled service from the compose graph — see Appendix A.

### 1.5 First bring-up

```bash
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod up -d --build
```

Watch Caddy obtain the certificate (this takes 30-90 seconds
for HTTP-01):

```bash
docker logs -f consigliere-caddy-prod 2>&1 | grep -E "obtained|error"
```

Expected line on success:
`certificate obtained successfully  identifier=<public-domain>  issuer=letsencrypt`

### 1.6 Smoke checks

```bash
# 1. HSTS header is set (proves Caddy is in the request path).
curl -I "https://${CADDY_DOMAIN}/" | grep -i strict-transport-security

# 2. The setup endpoint reports `setupRequired: true` on a
#    fresh install.
curl -sf "https://${CADDY_DOMAIN}/api/setup/status"

# 3. Liveness probe answers without auth.
curl -sf "https://${CADDY_DOMAIN}/health/live"
```

Each command should exit 0. Any non-zero exit means the
stack is not ready — re-check the docker logs before
proceeding to §2.

---

## 2. First-run setup wizard

After §1 the host responds at `https://<public-domain>` but
has no admin account. Visit `https://<public-domain>/setup`
in a browser and complete the four-step wizard:

1. **Admin account** — username + password + confirm. Pick a
   strong password (the rate limiter caps brute-force at
   5 attempts/minute per IP, but a weak password still
   reduces to seconds against an insider).
2. **Providers** — choose the primary realtime / raw-tx /
   REST providers. Bitails Websocket is the recommended
   default; the other paths exist as fallbacks.
3. **Block sync** — JungleBus base URL + block subscription
   ID. JungleBus subscription IDs are obtained from
   GorillaPool out-of-band.
4. **Review + confirm** — verify the assembled payload and
   submit.

On submit the wizard:

- Writes the admin credentials to RavenDB (hashed via
  `ConsigliereAdminPasswordHash`).
- Writes the provider configuration to
  `/var/lib/consigliere/secrets/providers.json` (owner-rw
  only — see §7).
- Redirects to `/login`.

Sign in once with the credentials you just created. If the
cookie does not persist after a successful login, check that
the cookie has `Secure; HttpOnly; SameSite=Lax` flags — the
production cookie policy refuses to set the cookie over plain
HTTP, so any reverse-proxy in front of Caddy must forward
`X-Forwarded-Proto: https`.

---

## 3. Scaling guidance

The current Consigliere stack is **single-instance by
design**. Horizontal scaling is a future deliverable —
rate-limit counters, the log stream ring, and the audit-log
expiration bundle all live in process memory and are not
cluster-aware yet.

### 3.1 Vertical sizing

Scale vertically before scaling horizontally. Recommended
ceilings for a single host:

| Workload class                              | vCPU | RAM   | Disk    | Notes |
|---------------------------------------------|------|-------|---------|-------|
| Light (≤ 100 watched addresses, ≤ 5 tps)    | 4    | 8 GB  | 100 GB  | First production cut. |
| Medium (≤ 1,000 watched addresses, ≤ 50 tps)| 8    | 16 GB | 250 GB  | Add an off-host nightly Raven backup (§8). |
| Heavy (≤ 10,000 watched addresses, ≤ 500 tps)| 16  | 32 GB | 500 GB  | Pin Raven to its own disk; consider RAID-1. |

### 3.2 Disk pressure

RavenDB is the only stateful component on the dev/prod
profiles. Watch the `raven-data` named volume for free space:

```bash
docker system df -v | grep raven-data
```

When the volume drops below 15 % free, decide between
expanding the disk and pruning the oldest blocks (the
projection cache is rebuildable; the journal is not).
Pruning procedures are out of scope for the runbook —
escalate to engineering.

### 3.3 Horizontal scaling

Out of scope for this release. Track the multi-instance HA
work in the platform roadmap.

---

## 4. Monitoring + alerting

Three anonymous health endpoints (no auth required so
orchestrator probes can hit them without a cookie):

| Path              | Pass criteria                                        | On failure |
|-------------------|------------------------------------------------------|------------|
| `/health/live`    | The process is listening                             | always 200 |
| `/health/ready`   | RavenDB + ≥ 1 configured provider reachable          | 503 |
| `/health/startup` | The DI graph resolved without throwing               | 503 |

Response JSON shape:

```json
{
  "status": "healthy",
  "checks": [
    { "name": "raven",     "status": "healthy",  "description": null, "durationMs": 12 },
    { "name": "providers", "status": "degraded", "description": "degraded: junglebus unreachable", "durationMs": 4810 }
  ]
}
```

`description` is intentionally terse — these endpoints are
unauthenticated, so they never leak hostnames, status codes,
or stack traces.

### 4.1 Wiring an external probe

Any HTTP-probe-capable monitor (Pingdom, UptimeRobot,
StatusCake, an in-cluster Prometheus blackbox exporter, etc.)
points at the three URLs:

```yaml
# Example k8s manifest fragment for inline probes.
livenessProbe:
  httpGet:
    path: /health/live
    port: 443
    scheme: HTTPS
  periodSeconds: 10
readinessProbe:
  httpGet:
    path: /health/ready
    port: 443
    scheme: HTTPS
  periodSeconds: 5
startupProbe:
  httpGet:
    path: /health/startup
    port: 443
    scheme: HTTPS
  failureThreshold: 30
  periodSeconds: 2
```

### 4.2 Recommended alert thresholds

| Endpoint          | Page on…                                              | Notify on… |
|-------------------|-------------------------------------------------------|-----------|
| `/health/live`    | non-200 for ≥ 2 consecutive probes                    | n/a (always paging-grade) |
| `/health/ready`   | non-200 for ≥ 3 consecutive probes (≈ 15 s)           | `degraded` for ≥ 5 minutes |
| `/health/startup` | non-200 after the first 60 s of a deploy              | startup > 30 s |

Route page-grade alerts to whichever paging system the team
runs (PagerDuty, Opsgenie, etc.) via the monitor's standard
webhook integration. Notify-grade alerts can drop into a
chat channel.

### 4.3 Caddy access log

The reverse proxy writes its access log to
`/var/log/caddy/` inside the `consigliere-caddy-prod`
container. To stream it:

```bash
docker logs -f consigliere-caddy-prod
```

Health-probe traffic is suppressed (the Caddyfile's
`log_skip @health` matcher hides `/health/*` requests so a
5-second probe cadence does not drown the log).

---

## 5. Log streaming

Two complementary surfaces.

### 5.1 In-browser live tail

Sign in to the admin UI and visit `/logs`. The screen
subscribes to the backend SignalR `/ws/logs` hub and displays
the latest 1,000 log entries with filter chips for minimum
level and category substring. The server-side sanitizer
redacts:

- `Authorization` + `Cookie` headers (through end-of-line)
- Bitcoin WIF private keys (51-52 char base58 starting with
  `K`, `L`, or `5`)
- JSON `apiKey` / `api_key` / `apikey` fields in both
  double- and single-quoted form
- Hex blobs longer than 128 characters (kept head + tail)

The same regex set is re-applied client-side as defence in
depth. Filter changes clear the local view, the server flushes
its ring snapshot to the cleared buffer, and the live stream
resumes with the new filters applied.

### 5.2 External log sink (optional)

For long-term retention or cross-host correlation, point
Serilog at an external sink (Loki, Elasticsearch, Datadog,
Seq, etc.) via `appsettings.Production.json`. Example
fragment for Grafana Loki:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "${LOKI_URL}",
          "labels": [
            { "key": "app",      "value": "consigliere" },
            { "key": "instance", "value": "${HOSTNAME}" }
          ],
          "propertiesAsLabels": ["SourceContext", "Level"]
        }
      }
    ]
  }
}
```

The `Serilog.Sinks.Grafana.Loki` package is already a
dependency, so no rebuild is required. Restart the
`consigliere` service after editing:

```bash
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod restart consigliere
```

---

## 6. Audit log forensics

Every destructive admin operation writes a forensic record
that is queryable through the admin UI's `/audit-log` screen.
Records are append-only — no surface in the product can
mutate or delete a row once it has landed.

### 6.1 Sample workflows

| Question                                              | How |
|-------------------------------------------------------|-----|
| Who pressed Force-Rebroadcast on tx `<txid>`?         | Filter `/audit-log` by `action = broadcast_tx`, search the Target column for the txid. |
| What did `<admin>` do between Friday 16:00 and 18:00? | Filter by `Username = <admin>` + `Since = 2026-05-23T16:00:00Z`. The grid is newest-first; oldest matching row at the bottom. |
| Was there a broadcast spike at 03:00 UTC?             | Filter `action = broadcast_tx`, set `Since` to the start of the window, look for clustered rows. |

The "View JSON" button opens the `context` blob. For
`broadcast_tx` the context shape is
`{ "rawHexLength": <n>, "source": "admin-ui" }` — the raw
transaction bytes are **never** stored.

### 6.2 Retention

Audit records carry a Raven `@expires` metadata header set
to `+365 days` at write time. The expiration bundle is
enabled lazily on the first audit write; if Raven refuses
the bundle the destructive operation that triggered the
write is also aborted (the audit log is fail-stop). To
extend retention beyond 365 days, edit each entry's
`@expires` metadata via the Raven Studio before the TTL
fires.

### 6.3 Export

`/audit-log` does not have a CSV export button. To export
for an external case file, use Raven's Studio against the
`AuditLogEntries` collection:

1. Open the Raven Studio at `http://<raven-host>:8080`
   (only reachable on the internal docker network — port-
   forward from the host first).
2. Open the `AuditLogEntries` collection.
3. Filter via the query bar; right-click selected rows →
   Export to JSON.

For automated extraction, point any Raven Smuggler-friendly
tool at the same collection (the same path used in §8 for
backup).

---

## 7. Secrets rotation

### 7.1 Admin password

Re-run the setup wizard at `https://<public-domain>/setup`
**with the same username**. The wizard treats a re-submit as
a credential rotation:

1. RavenDB document `setup/bootstrap` is updated with the
   new password hash.
2. Any existing admin sessions remain valid until their TTL
   expires (480 minutes default) — to force immediate
   re-authentication, restart the `consigliere` service
   after the rotation:

```bash
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod restart consigliere
```

### 7.2 Provider API keys

Provider credentials live in
`/var/lib/consigliere/secrets/providers.json` (chmod 600).
Two paths to update them:

**Option A — through the admin UI (recommended)**

1. Sign in, visit `/providers`.
2. Edit the relevant provider's API key field.
3. Save. The file is rewritten atomically (tmp + rename).

**Option B — direct file edit**

```bash
# Stop the host so the file write is uncontested.
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod stop consigliere

# Edit in place inside the secret volume.
docker run --rm -it \
  -v dxs-consigliere_consigliere-secrets:/secrets \
  alpine:3 vi /secrets/providers.json

# Start the host.
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod start consigliere
```

### 7.3 RavenDB credentials

The bundled `ravendb` compose service runs in unsecured
mode on the docker internal network (no password, no
cert). Nothing to rotate.

For deployments that swap the bundled service for an
external Raven cluster with cert-based auth, the rotation
procedure is cluster-specific (re-issue client cert, update
`RavenDb__ClientCertificate` env var, restart consigliere).
That path is out of scope for this runbook — escalate to
engineering for cluster-specific procedures.

### 7.4 TLS certificates

The Caddy ACME workflow rotates Let's Encrypt certificates
automatically every 60 days. No operator action required
under normal conditions.

To force a renewal (for example, after a domain change):

```bash
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod down caddy-prod
docker volume rm dxs-consigliere_caddy-data
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod up -d caddy-prod
```

> **Warning.** Removing `caddy-data` clears the ACME account
> + all cached certs. The next bring-up will re-register with
> Let's Encrypt and re-issue. Do this only when you have
> confirmed DNS resolves to the host — otherwise the
> rate-limit window closes for 168 hours.

---

## 8. Disaster recovery

The only stateful surface is RavenDB. Cert + ACME state on
the `caddy-data` volume is recoverable from Let's Encrypt
(at the cost of one issuance round-trip). Secrets in
`consigliere-secrets` are recoverable by re-running the
setup wizard.

### 8.1 Backup setup (run once per deployment)

Schedule a nightly Raven Smuggler export to an off-host
location. Example cron entry (run as the deploy user):

```cron
# nightly Raven backup at 02:00 local time
0 2 * * * docker exec consigliere-ravendb \
  /opt/RavenDB/Server/Raven.Server smuggler export \
  --url http://localhost:8080 \
  --database Consigliere \
  --file /var/backups/consigliere-$(date +\%F).ravendbdump \
  && aws s3 cp /var/backups/consigliere-$(date +\%F).ravendbdump \
       s3://<backup-bucket>/consigliere/
```

Retain dumps for 30 days minimum. Test the restore (§8.3)
quarterly.

### 8.2 What's NOT in the backup

- The `data/secrets/providers.json` file (recoverable via
  the setup wizard or admin UI).
- Caddy's ACME state (recoverable on next bring-up if DNS
  resolves correctly).
- The audit log's `@expires` metadata — Smuggler exports
  the metadata so the 365-day TTL transfers cleanly to the
  restore target.

### 8.3 Restore procedure (RTO target: 30 min)

```bash
# 1. Provision a fresh VM per §1.1. Set DNS to point at it.
# 2. Install Docker + Compose per §1.2.
# 3. Clone the repo + restore the `.env` from your secret manager.
# 4. Bring up RavenDB ONLY (no Consigliere yet).
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod up -d ravendb

# 5. Wait for Raven to be ready.
until docker exec consigliere-ravendb \
  curl -sf http://localhost:8080/admin/server/info; do sleep 2; done

# 6. Restore the most recent Smuggler dump.
aws s3 cp s3://<backup-bucket>/consigliere/<filename>.ravendbdump \
  /tmp/restore.ravendbdump
docker exec -i consigliere-ravendb \
  /opt/RavenDB/Server/Raven.Server smuggler import \
  --url http://localhost:8080 \
  --database Consigliere \
  --file /tmp/restore.ravendbdump

# 7. Bring up the rest of the stack.
docker compose -f compose.yml -f compose.prod.yml \
  --profile prod up -d

# 8. Verify §1.6 smoke checks pass.
# 9. Sign in with the admin account from the restored state.
# 10. Re-run the setup wizard to rewrite providers.json from
#     the admin UI (or restore the JSON from a secret manager).
```

If any step in this procedure fails, **escalate to
engineering** before mutating further state — a half-restored
Raven can be harder to recover than a clean restart.

---

## Appendix A — Cookie + forwarded headers

The session cookie's `Secure` flag is config-driven via
`Consigliere:AdminAuth:cookieSecure` (env var
`Consigliere__AdminAuth__cookieSecure`):

| Environment              | Value           |
|--------------------------|-----------------|
| Production               | `Always`        |
| DockerComposeE2E (dev)   | `SameAsRequest` |
| Test (contract suite)    | `SameAsRequest` |

The parser is conservative: any unknown / null / empty value
maps to `Always`. A misconfigured env var cannot silently
drop the Secure flag.

Caddy forwards the original scheme + client IP via
`X-Forwarded-Proto` and `X-Forwarded-For`. Kestrel honours
both via `app.UseForwardedHeaders()`.

By default Kestrel trusts the forwarded headers from any
immediate caller — safe ONLY because Caddy overwrites
`X-Forwarded-For` with the real client IP and Kestrel is
not published to the host (compose `expose`, not `ports`).
If you publish `:5000` directly, drop Caddy, or front the
stack with another LB, restrict which hop is trusted via
**string** config (these bind from env, unlike the
framework's `IPAddress`-typed lists):

```sh
# Trust only the docker bridge network (example CIDR):
export Consigliere__ForwardedHeaders__KnownNetworks__0=172.16.0.0/12
# …or a specific upstream proxy IP:
export Consigliere__ForwardedHeaders__KnownProxies__0=10.0.0.5
```

When at least one entry is set, ONLY those proxies/networks
are trusted. Malformed entries are skipped (they don't
crash startup).

## Appendix B — Rate limiting

Three per-IP fixed-window policies:

| Endpoint                       | Default limit  |
|--------------------------------|---------------|
| `POST /api/admin/auth/login`   | 5 / minute    |
| `GET /api/admin/auth/me`       | 100 / minute  |
| `POST /api/tx/broadcast`       | 1 / second    |

Health endpoints (`/health/*`) opt out via
`.DisableRateLimiting()`. Loosen any limit via the
`RateLimiting:*` env vars:

```bash
export RateLimiting__Login__PermitsPerMinute=10
export RateLimiting__Broadcast__PermitsPerSecond=5
```

Restart the `consigliere` service to pick up the change.
The limiter is per-process; multi-instance HA is on the
future roadmap.

## Appendix C — CI grep gate

`scripts/secrets-lint.sh` runs on every PR + push (CI job
`secrets-lint`). It greps
`src/Dxs.Consigliere/appsettings*.json` for
`"ApiKey" | "Password" | "Secret" | "Token"` keys whose
value starts with anything other than `"`, `${`, or `{`.
Re-introducing a plaintext credential into the JSON files
fails the build.
