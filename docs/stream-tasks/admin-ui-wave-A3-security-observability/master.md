---
created: 2026-05-19
type: wave
status: approved (planning only — no slices executed yet)
parent: docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/
related: docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/evidence/closeout.md (wave-A2 residuals);
         docs/stream-tasks/admin-ui-vnext-program/evidence/closeout.md (wave-A1 closeout);
         /Users/imighty/Code/docs/frontend-principles.md
---

# Admin UI Wave A3 — Security + observability baseline

Wave-A3 turns the admin UI + backend into something that can
run for a real paying customer on the open internet without
needing 24/7 hand-holding. It closes the four production
blockers identified after wave-A2's smoke install:

- **No TLS** — `docker compose up` exposes HTTP only.
- **No rate limiting** — `/api/admin/auth/login` accepts
  unlimited attempts.
- **No probes** — orchestrators (k8s / Railway / systemd)
  have no way to ask "are you up?" beyond "the port
  answers."
- **No audit trail** — Force-rebroadcast (the only
  destructive action) leaves no record of who did it,
  when, or against what tx.

Plus three quieter blockers that hurt SRE ergonomics:

- **No log surface in the UI** — the wave-A1 `/logs`
  screen is a paste box; operators still need shell access
  to journalctl.
- **Plaintext secrets in git** — `appsettings.*.json`
  pulls API keys and Raven creds inline; an `appsettings.
  Production.json` doesn't exist yet.
- **DTO contract still leaky** — wave-A2 S1 + S2 caught
  drift, but the SCREENS still consume hand-mirrored
  types. A backend rename fires CI red but doesn't
  produce a compile error inside the screens — only the
  generated file changes.

## Goal

A Consigliere deployment that an operator can stand up
behind a public DNS name, run for weeks unattended, and
fully forensically reconstruct after an incident — with
the admin UI as the only surface they need to touch.

Business outcome:
- First real-customer deployment unblocked.
- Incident postmortems answerable from inside the product.
- No ops escalation for "what was the last broadcast that
  failed?" — the audit log answers it.

## Product Decision

- **TLS at the reverse proxy, not in Kestrel.** Caddy in
  the compose stack handles certificate provisioning
  (Let's Encrypt or self-signed for dev). Kestrel keeps
  listening on plain HTTP inside the docker network. This
  avoids embedding certificate-store handling in the
  application code, which is a class of CVE we'd rather
  not own.
- **Rate limiting via the framework, not a custom
  decorator.** `Microsoft.AspNetCore.RateLimiting` (built
  into .NET 7+) gives token-bucket + fixed-window
  primitives that compose with the existing
  `[Authorize(Policy = AdminAuthDefaults.Policy)]`
  attribute. No new NuGet dep.
- **Health checks via `Microsoft.Extensions.Diagnostics.
  HealthChecks`.** Three endpoints (`/health/live`,
  `/health/ready`, `/health/startup`); each maps to a
  named tag so the framework's check registry drives the
  HTTP response.
- **Audit log is append-only in RavenDB.** New collection
  `AuditLogEntries` with retention via TTL document
  expiry (default 365 days). UI reads with the same
  filtering primitives the alerts grid uses (wave-A1 S7).
- **Log streaming via Serilog → SignalR.** Reuses the
  `/ws/consigliere` hub auth model (cookie-mode session,
  admin-only). Ring buffer is in-process, no Raven write
  on the hot path.
- **Secrets at rest = env vars + Docker secrets, NOT
  Raven encryption.** Wave-A3 doesn't try to encrypt
  Raven-side; that's a larger compliance topic. We just
  make sure secrets never enter git or process memory
  via plaintext config files.
- **Swashbuckle NRT inference is one slice, screen
  migration is the next.** Splitting because the NRT
  filter is backend-only + the migration is a frontend
  cascade. Either can ship independently.
- **Operator runbook is the closeout artifact.** Without
  a written runbook, the deployment is "Claude knows how
  to fix it" — that's not shippable.

## Scope

In scope:
- Caddy reverse-proxy in compose; Let's Encrypt + dev
  self-signed cert paths
- `Microsoft.AspNetCore.RateLimiting` middleware on the
  auth endpoints (login + me) and the destructive
  endpoint (`/api/tx/broadcast`)
- `/health/{live,ready,startup}` endpoints with named
  tags; HealthChecks for Raven + external providers
- `AuditLogEntry` Raven document + `IAuditLogger` service
  + `AuditLogger` impl that EVERY destructive code path
  must call (currently just `BroadcastService.broadcast`,
  but the interface is shaped for future config writes)
- `/audit-log` admin UI screen with DataGrid + filters
- Serilog `LogStreamSink` (in-process ring buffer) +
  SignalR hub method `SubscribeToLogs` + frontend live
  tail in `/logs` screen (replaces the wave-A1 paste box)
- `appsettings.Production.json` (new) with `${SECRET}`-
  style env var binding; CI lint gate that asserts no
  plaintext keys
- Setup wizard from wave-A2 S0 writes provider config to
  `data/secrets/providers.json` (chmod 600), NOT a Raven
  document with plaintext fields
- Swashbuckle swagger gen filter that walks C# nullability
  annotations + populates OpenAPI `required` arrays
- Frontend migration: `types/admin.ts` + `types/auth.ts`
  re-export from `api.generated.ts`; screens compile
  cleanly against the now-stricter types
- `docs/runbook.md` — deployment, scaling, on-call, DR

Out of scope:
- Multi-instance HA (wave-A4)
- Circuit breakers + retry policies for upstreams
  (wave-A4)
- RavenDB nightly backup (wave-A4)
- RBAC + multi-user accounts (wave-A5)
- OpenTelemetry distributed tracing (wave-A6)
- Encryption at rest in Raven (compliance wave)
- IPv6 / split-horizon DNS / WAF rules — operator's
  network problem, documented in the runbook

## Core Rules

1. **No back-compat shims.** The setup wizard's
   `data/secrets/providers.json` is the canonical secrets
   location. The wave-A2 path that wrote provider config
   to a Raven document is REPLACED, not preserved
   side-by-side. No `// legacy path kept for transition`
   comments.
2. **TLS termination has ZERO C# code.** Caddy +
   `appsettings.Production.json` flips the cookie
   `Secure` flag — that's the only change to the
   application code. Forwarded-headers middleware is
   already in the stack from wave-A1; we just enable it.
3. **Audit log writes are not optional on destructive
   paths.** `IAuditLogger.RecordAsync(...)` MUST be
   called BEFORE the destructive operation commits. If
   the write fails, the operation does NOT proceed
   (fail-stop, not fail-loud). The frontend
   force-rebroadcast dialog already two-step-confirms;
   the audit write is the third step (invisible to the
   operator).
4. **Health endpoints are anonymous.** Required for k8s
   readiness probes that don't ship a cookie. They expose
   nothing the cluster status doesn't already expose
   (binary up/down + the names of failing health checks,
   never the underlying secret).
5. **Log streaming is admin-only.** Cookie-mode session
   required. Logs may contain transaction IDs,
   addresses, peer endpoints — none of that is public.
6. **Rate limit defaults err on the conservative side.**
   5 login/min/IP, 100 me/min/IP, 1 broadcast/sec/IP.
   Override via env vars; document in runbook. The first
   real-customer deployment can tune these higher; better
   to start tight + relax than the other way around.
7. **Swashbuckle NRT inference is a swagger-gen filter,
   not a global DTO rewrite.** No `[Required]` attributes
   sprinkled across the codebase. C# nullability
   annotations on the DTO properties drive the
   `required` array of each schema. Wave-A2's drift gate
   stays in place; the NRT-tightened generated file just
   has fewer `?:` properties.
8. **No new backend public DTOs.** New shapes are
   `AuditLogEntry` (internal Raven doc) + admin
   `AuditLogResponse` (new wire shape, audited normally).

## Ownership Zones

| Wave zone | Repo zone | Files |
|---|---|---|
| `tls-proxy` | `runtime-and-infrastructure` | New: `compose.yml` (Caddy service), `infrastructure/caddy/Caddyfile`, `infrastructure/caddy/snippets/*.caddy`; updated: `src/Dxs.Consigliere/Startup.cs` (ForwardedHeaders middleware enabled), `src/Dxs.Consigliere/Setup/AdminAuthSetup.cs` (cookie `Secure` flag from config) |
| `rate-limiting` | `bsv-runtime` | `src/Dxs.Consigliere/Setup/PublicApiSetup.cs` (RateLimiter middleware registration), `src/Dxs.Consigliere/Setup/RateLimiterPolicies.cs` (new — policy definitions); `src/Dxs.Consigliere/Configs/RateLimitingConfig.cs` (new) |
| `health-checks` | `core-platform` | `src/Dxs.Consigliere/Setup/HealthChecksSetup.cs` (new); RavenDB + Bitails + WhatsOnChain + JungleBus probes |
| `audit-trail` | `persistence` + `admin-ui` | New: `src/Dxs.Consigliere/Data/Models/Audit/AuditLogEntry.cs`, `src/Dxs.Consigliere/Services/Audit/{IAuditLogger,AuditLogger}.cs`, `src/Dxs.Consigliere/Controllers/AdminAuditController.cs`; updated: `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs` (records on broadcast); new admin UI: `src/admin-ui/src/screens/audit-log/AuditLogPage.tsx` + store + tests |
| `log-streaming` | `observability` (new) | New: `src/Dxs.Consigliere/Logging/LogStreamSink.cs` (Serilog sink), `src/Dxs.Consigliere/WebSockets/LogStreamHub.cs` (auth-gated); updated: `src/admin-ui/src/screens/logs/LogsPage.tsx` (live tail replaces paste box) |
| `secrets-at-rest` | `runtime-and-infrastructure` | New: `src/Dxs.Consigliere/appsettings.Production.json`, `src/Dxs.Consigliere/Data/SecretsFileStore.cs`; updated: setup-wizard write path (writes to `data/secrets/providers.json`, NOT a Raven doc); CI lint that grep-rejects plaintext keys |
| `contract-tightening` | `public-api` + `admin-ui-foundation` | New: `src/Dxs.Consigliere/Swagger/RequiredFromNrtFilter.cs` (Swashbuckle ISchemaFilter); updated: `src/admin-ui/src/types/{admin,auth}.ts` (re-export from generated), every screen that referenced a hand-mirrored shape (no behavioral change, just the import source flips) |
| `runbook` | `repo-governance` | New: `docs/runbook.md` (deployment, scaling, on-call, DR); updated: README.md ops section |
| `wave-docs` | `repo-governance` | `docs/stream-tasks/admin-ui-wave-A3-security-observability/*` |

## Wave Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | `tls-proxy` | **done** | — | `docker compose --profile dev config` parses; HSTS snippet emitted by both Caddyfiles; cookie SecurePolicy parser unit-tested (8 known + 5 unknown-defaults-Always cases); ForwardedHeaders wiring unit-tested; wave-A2 contract suite remains 16/16 green | Caddy in compose under `dev`/`prod` profiles (mutually exclusive); Caddyfile.dev `tls internal`, Caddyfile.prod ACME via `${CADDY_DOMAIN}`+`${CADDY_EMAIL}`; Kestrel + Raven both moved to `expose` only (no host publishing); cookie `Cookie.SecurePolicy` bound from `Consigliere:AdminAuth:cookieSecure` (default `Always`, conservative parser); `app.UseForwardedHeaders()` before `UseCors`/`UseRouting`; runbook TLS section landed | `audits/S0-slice-audit-prompt.md` |
| S1 | `rate-limiting` | todo | S0 (so the Forwarded-For header arrives correctly) | A burst of 10 logins in <1s from one IP yields 5×200/302 + 5×429 with `Retry-After`; integration test pins the limits | `Microsoft.AspNetCore.RateLimiting` registered; 3 policies (`login`, `me`, `broadcast`) attached to their endpoints; per-policy limits in `RateLimitingConfig` + overridable via env | `audits/S1-slice-audit-prompt.md` |
| S2 | `health-checks` | todo | — (parallel with S0/S1) | `curl http://localhost:5000/health/live` returns 200 always; `/health/ready` 200 when Raven + at least one provider reachable, 503 otherwise; `/health/startup` 200 after DI graph resolves | Three endpoints registered; named tags (`live`, `ready`, `startup`); RavenDB + Bitails + WoC + JungleBus probes; documented in the runbook | `audits/S2-slice-audit-prompt.md` |
| S3 | `audit-trail` | todo | — (parallel) | `BroadcastService.BroadcastAsync(...)` writes an `AuditLogEntry` BEFORE attempting the network send; the entry is queryable from `/api/admin/audit-log`; new vitest contract describes the response shape | New Raven collection + admin REST endpoint + UI screen (DataGrid with filters by user / action / time range); broadcast audit pins every Force-rebroadcast call from the UI; spec proves an audit entry lands per dialog confirmation | `audits/S3-slice-audit-prompt.md` |
| S4 | `log-streaming` | todo | S3 (shares hub auth pattern) | Operator opens `/logs`, sees the latest 1000 entries scroll live as the backend logs; filtering by category / level works; refresh-resilient | Serilog SignalR sink + `LogStreamHub.SubscribeToLogs(...)` + admin-ui live-tail panel replaces the paste box; wave-A1 S10 sanitizer still applies (server-side now, not client) | `audits/S4-slice-audit-prompt.md` |
| S5 | `secrets-at-rest` | todo | S0 (Caddy reads the cert path from env) | `grep -r "ApiKey\":\s*\"[^$]" src/Dxs.Consigliere/appsettings.*.json` returns ZERO matches; setup wizard writes to `data/secrets/providers.json` (chmod 600); existing Raven path is REMOVED, not parallel | `appsettings.Production.json` exists + uses `${ENV_VAR}` placeholders; CI grep gate catches plaintext leak; setup wizard's submit writes to file + the existing provider-config Raven document is DELETED; runbook documents the env vars + Docker secrets path | `audits/S5-slice-audit-prompt.md` |
| S6 | `contract-tightening` | todo | wave-A2 S1 (codegen pipeline) | `pnpm contracts:generate` produces an `api.generated.ts` where non-nullable C# properties are `required` (no `?:`); `pnpm contracts:check` passes; screens compile against the now-stricter types with ZERO `T \| undefined` cascades because hand-mirrored interfaces are the bridge during cutover; once `required` arrays exist, AJV in `_schema-validator.ts` will catch dropped/renamed fields too — closes wave-A2 S2-audit L2 | Swashbuckle `RequiredFromNrtFilter` registered; generated types tightened; `types/{admin,auth}.ts` re-export wire DTOs from `api.generated.ts`; screen-side compile is clean; the `required:` arrays in `swagger.json` populate for non-nullable C# props (manual diff post-generate) | `audits/S6-slice-audit-prompt.md` |
| S7 | `runbook` | todo | S0..S6 (codifies the deployment as it actually shipped) | A new operator who has never seen Consigliere can read `docs/runbook.md` and: (1) deploy to a fresh VM, (2) recover from a Raven outage, (3) rotate the admin password, (4) restore from backup. Each procedure is a numbered checklist with copy-pasteable commands. | `docs/runbook.md` covers deployment / scaling / on-call / DR / secret rotation; README.md links it; wave A1+A2+A3 closeout docs link it as the operations source of truth | `audits/S7-slice-audit-prompt.md` |

## Definition of Done

- All 8 slices `done`; each has a corresponding
  `audits/S<n>-slice-audit-followup.md` with verdict
  `APPROVE` or `APPROVE WITH CHANGES (folded)`.
- `pnpm verify` chain + `pnpm test:contract` + `pnpm
  test:e2e` all green; new e2e for the audit-log
  happy-path + the rate-limit 429 surface.
- `dotnet build -c Release` clean; new unit tests for
  `AuditLogger`, `RateLimiterPolicies`, the
  `RequiredFromNrtFilter`.
- A fresh `docker compose up --build` against a clean
  RavenDB volume + a Caddy `Caddyfile.dev` (self-signed)
  lands the operator on `https://localhost/setup` and
  the wizard completes against the TLS-fronted backend.
- `curl -i https://localhost/health/live` returns 200 +
  `strict-transport-security: max-age=...` header.
- Audit log records a row for every Force-rebroadcast
  dialog confirmation; new `/audit-log` screen shows it.
- `docs/runbook.md` reviewed by a non-developer (e.g.
  the human operator running the smoke deploy) — they
  can stand up the stack from scratch reading only the
  runbook.
- Wave audit (`audits/A1.md`) + `evidence/closeout.md`
  written per the wave-A2 template.

## Delivery Notes

Per-slice commit hashes recorded here at closeout:

| slice | commit | summary |
|---|---|---|
| S0 | `ade41b0` | Caddy TLS termination (dev `tls internal` + prod ACME profiles, mutually exclusive) + cookie `SecurePolicy` config knob (default `Always`, conservative parser) + ForwardedHeaders middleware + runbook TLS section |
| S0 audit fold | `58b0684` | Codex MAJOR REVISION REQUIRED fold: M1 split caddy-prod to `compose.prod.yml` override so `--profile dev config` stops failing on prod-only env interpolation; M2 dev Caddyfile binds `https://localhost` + `https://127.0.0.1` as concrete site addresses so `tls internal` issues a cert that curl can complete a handshake against; L1 ledger hash backfill (`8ba09a5` → `ade41b0` after amend); L2 drop `ForwardedHeadersOptions__KnownProxies__0` env-var suggestion from runbook (`IList<IPAddress>` does not round-trip via `IConfiguration` binding). |
| S1 | _pending_ | _AspNetCore.RateLimiting on auth + broadcast_ |
| S2 | _pending_ | _Health endpoints + named-tag probes_ |
| S3 | _pending_ | _AuditLogEntry + /audit-log admin UI screen_ |
| S4 | _pending_ | _Serilog → SignalR log stream + UI live tail_ |
| S5 | _pending_ | _appsettings.Production + Docker secrets + setup-wizard file write_ |
| S6 | _pending_ | _Swashbuckle NRT filter + screen migration_ |
| S7 | _pending_ | _docs/runbook.md operator handbook_ |
| Audit folds | _pending_ | _per-slice findings folded_ |
