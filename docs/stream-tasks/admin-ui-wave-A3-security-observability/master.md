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
| S1 | `rate-limiting` | **done** | S0 (so the Forwarded-For header arrives correctly) | `dotnet test --filter RateLimiting.RateLimiterPoliciesTests` 6/6 green; `pnpm test:contract` 23/23 green (was 20; +3 rate-limit burst describes against a strict-limit host spawned via the new `envOverrides` knob — 10-login burst yields exactly 5×non-429 + 5×429 with `Retry-After`, /me stays on its own partition, /health/live stays opted out); `pnpm verify` 182/182 unit incl. 4 new ApiClient retry-once cases | `Microsoft.AspNetCore.RateLimiting` registered via `AddConsigliereRateLimiting`; three IP-keyed fixed-window policies (`login` 5/min, `me` 100/min, `broadcast` 1/sec — conservative defaults from `RateLimitingConfig`, overridable via `RateLimiting:*` env); `OnRejectedAsync` emits `Retry-After` from lease metadata; `app.UseRateLimiter()` slotted after auth + before endpoint dispatch; controller attributes `[EnableRateLimiting("login"|"me"|"broadcast")]`; `MapHealthChecks` chains `.DisableRateLimiting()` so probes don't 429; admin-ui `ApiClient` honours `Retry-After` exactly ONCE (clamped to 65s) then throws `AppError { category: "RateLimited" }`; new `"RateLimited"` `AppErrorCategory`; runbook gains a rate-limiting section + env-var override examples | `audits/S1-slice-audit-prompt.md` + `audits/S1-slice-audit.md` (codex APPROVE, 0 findings) |
| S2 | `health-checks` + audit fold | **done** | — (parallel with S0/S1) | `dotnet test --filter Health.HealthChecksSetupTests\|Health.ProviderReachabilityCheckTests` 11/11 green (was 9; +2 cases after the audit fold); `pnpm test:contract` 20/20 green (was 16; +4 `health.test.ts` describes hitting all three endpoints anonymously against the real ASP.NET host); `/health/live` returns 200 + empty checks; `/health/ready` lists `raven` + `providers` with the slice's verdict matrix; `/health/startup` resolves `IBroadcastService` to prove the DI graph is up | Three anonymous endpoints registered; named tags (`live`/no-check, `ready`, `startup`); `RavenHealthCheck` (2s timeout, terse description), `ProviderReachabilityCheck` (5s per-target HEAD, Healthy/Degraded/Unhealthy matrix, no URL leakage; S2-audit M1 fix: sources targets from `IAdminProviderConfigService.GetEffectiveSourcesConfigAsync()` not `IOptionsMonitor`, honours `Enabled` flag, Degrades on config-unavailable instead of double-counting Raven failures), `StartupDiCheck` (resolves `IBroadcastService` without touching Raven); shared `HealthResponseWriter` emits `{status, checks:[{name,status,description,durationMs}]}`; Caddy `prod` `@health path /health/*` matcher + `log_skip` keeps probe noise out of the access log + holds the slot for S1's rate-limit bypass; runbook gains a probe section + sample k8s YAML | `audits/S2-slice-audit-prompt.md` + `audits/S2-slice-audit-followup.md` (codex MAJOR REVISION REQUIRED → 1/1 folded) |
| S3 | `audit-trail` | **done** | — (parallel) | `dotnet test --filter Broadcast.BroadcastServiceBehaviorTests` 8/8 (was 6; +2 audit fail-stop pins); `pnpm test:contract` 24/24 (was 23; +1 audit-log describe); `pnpm verify` green incl. 5 new `audit-log.store` unit cases; manual mock-mode smoke renders the seeded entries with the View-JSON modal | `AuditLogEntry` Raven document (`Entity` not `AuditableEntity` — audit IS the trail; `UpdateableKeys() == EmptyKeys`); `IAuditLogger.RecordAsync(action, targetId, context, username?)` returns `Task<bool>` so the caller can fail-stop; `AuditLogger` opens a short-lived session, sets `@expires = +365d`, lazily enables the expiration bundle; `BroadcastService` ctor takes `IAuditLogger` and aborts the broadcast with `audit_write_failed` if the record fails (zero persist + zero announce); `AdminAuditController` `GET /api/admin/audit-log` admin-authed with `since|action|username|lastN` filters + 1000-row hard ceiling + newest-first ordering; admin-ui `audit-log` screen with MUI DataGrid + filter chips + JSON-context modal under the System section | `audits/S3-slice-audit-prompt.md` + `audits/S3-slice-audit.md` + `audits/S3-slice-audit-followup.md` (codex MAJOR REVISION REQUIRED → 2/2 folded) |
| S4 | `log-streaming` | **done** | S3 (shares hub auth pattern) | `dotnet test --filter Logging` 12/12 green (7 sanitizer + 5 ring-buffer); `pnpm verify` green incl. 8 retargeted sanitizer pins + 5 new `logs.store` cases; `pnpm test:contract` 24/24 still green; manual smoke shows backend INFO emissions arriving in the admin UI live tail | `LogStreamBuffer` (1000-entry ring, drop-oldest, fan-out fail-soft); `LogStreamProvider` MEL `ILoggerProvider` captures every `ILogger<T>` emission and pipes through the server-side `LogSanitizer` (regex set ported from wave-A1 admin-ui paste box); `LogStreamHub` `[Authorize(Policy = AdminAuthDefaults.Policy)]` at `/ws/logs` flushes the ring + subscribes to live emissions per connection with min-level + category-substring filters; admin-ui `LogsPage` rewritten — paste box gone, MUI live tail of the latest 1000 entries with filter chips + clear/refresh icons; sanitizer regex set extracted into `screens/logs/sanitizer.ts` and re-applied client-side as defence-in-depth | `audits/S4-slice-audit-prompt.md` + `audits/S4-slice-audit.md` + `audits/S4-slice-audit-followup.md` (codex MINOR REVISION REQUIRED → 1/1 folded) |
| S5 | `secrets-at-rest` | **done** | S0 (Caddy reads the cert path from env) | `bash scripts/secrets-lint.sh` at HEAD exits 0; `dotnet test --filter Secrets` 6/6 green + 3 RavenTestDriver migration cases (skip without .NET 8 locally); `pnpm test:contract` 23/23 green against a host writing to `data/secrets-test/`; `pnpm verify` green; CI `secrets-lint` job blocks future regressions | `SecretsFileStore` replaces the Raven-backed `RealtimeSourcePolicyOverrideStore` (deleted); chmod-600 atomic-write via `tmp + Move(overwrite:true)`; `MigrateFromRavenAsync` is the one-shot wave-A2 → wave-A3 cutover (file write → Raven delete, idempotent), runs from `Startup.InitializeDatabase` fail-stop; `appsettings.Production.json` lands with `${ENV_VAR}` placeholders for `RavenDb__Urls__0` + `RavenDb__DbName`; `compose.yml` adds the `consigliere-secrets` volume + `Consigliere__Secrets__Dir=/var/lib/consigliere/secrets` env; `scripts/secrets-lint.sh` + new CI job grep-gate `"ApiKey\|Password\|Secret\|Token"` keys against plaintext; runbook gains secrets-at-rest section (layout / migration / rotation / CI grep gate) | `audits/S5-slice-audit-prompt.md` + `audits/S5-slice-audit.md` + `audits/S5-slice-audit-followup.md` (codex MAJOR REVISION REQUIRED → 2/2 folded) |
| S6 | `contract-tightening` | **done (partial; residual)** | wave-A2 S1 (codegen pipeline) | `dotnet test --filter Swagger.RequiredFromNrtFilterTests` 5/5; `pnpm contracts:generate` adds many `required` arrays (293 new lines in swagger.json) + flips 187 generated-TS lines from `?: T \| null` to `: T`; `pnpm verify` green; `pnpm test:contract` 24/24 green; wave-A2 S2-audit L2 (AJV gate had no `required` to enforce) becomes effective for every NotNull property | `RequiredFromNrtFilter` `ISchemaFilter` walks NRT annotations (`WriteState`/`ReadState == NotNull`) + populates `schema.Required` AND flips `OpenApiSchema.Nullable = false` so openapi-typescript drops the `\| null` union; `S3 AdminAuditLog*` DTOs migrated as the first concrete `#nullable enable` example + re-exported from `api.generated.ts` in `types/admin.ts`. RESIDUAL (wave-A4): the wider hand-mirrored-interface sweep across the remaining ~30 Dto.cs files is logged in the slice's audit-prompt — the migration pattern is established + the per-file recipe documented | `audits/S6-slice-audit-prompt.md` + `audits/S6-slice-audit.md` + `audits/S6-slice-audit-followup.md` (codex APPROVE WITH CHANGES → 1/1 folded) |
| S7 | `runbook` | **done** | S0..S6 (codifies the deployment as it actually shipped) | `docs/runbook.md` covers all 8 mandated sections with copy-pasteable commands + angle-bracket placeholder convention; `README.md` Ops section + wave A1 + A2 closeouts point at it as the operator-facing source of truth; obsolete `docs/admin-ui/design-handoff/03-prod-runbook.md` stub deleted; design-handoff README entry rewritten to point at the new runbook; `pnpm verify` + `pnpm test:contract` still green | 8 sections: first-time deployment / first-run wizard / scaling / monitoring + alerting / log streaming (live tail + optional external sink) / audit log forensics / secrets rotation (admin pw / provider API keys / Raven pw / TLS) / disaster recovery (Smuggler nightly + 30-min RTO restore); 3 appendices: cookie + forwarded headers, rate-limiting policy table, CI grep-gate. Stopwatch (non-engineer reads ONLY the runbook + stands up the stack < 45 min) is the wave-A3 closeout verification step. | `audits/S7-slice-audit-prompt.md` + `audits/S7-slice-audit.md` + `audits/S7-slice-audit-followup.md` (codex MAJOR REVISION REQUIRED → 3/3 folded) |

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
| S1 | `4c3e7ae` | `Microsoft.AspNetCore.RateLimiting` on `/api/admin/auth/{login,me}` + `/api/tx/broadcast` (5/min, 100/min, 1/sec conservative defaults — env-overridable); IP-partitioned fixed-window via `RateLimiterPolicies.ToPartitionFactory`; `Retry-After` emitted from lease metadata; `HealthChecks` chain `.DisableRateLimiting()`; admin-ui `ApiClient` honours `Retry-After` once (≤65s clamp) then throws `RateLimited` `AppError`; host-harness gains `envOverrides` for the burst test; runbook + audit prompt landed |
| S2 | `5efdff2` | `/health/{live,ready,startup}` anonymous probes — `Microsoft.Extensions.Diagnostics.HealthChecks` + named tag predicates; `RavenHealthCheck` (2s timeout, terse description), `ProviderReachabilityCheck` (HEAD 5s/target, Healthy/Degraded/Unhealthy matrix, no URL leakage), `StartupDiCheck` (resolves `IBroadcastService` without touching Raven); shared `HealthResponseWriter`; Caddy `prod` `log_skip @health` matcher; runbook probe section + sample k8s YAML; 9/9 backend unit + 4/4 vitest contract describes |
| S2 audit fold | `37a5b31` | Codex MAJOR REVISION REQUIRED fold: M1 `ProviderReachabilityCheck` now sources targets from `IAdminProviderConfigService.GetEffectiveSourcesConfigAsync()` instead of `IOptionsMonitor<ConsigliereSourcesConfig>`, honours per-provider `Enabled` flag, and Degrades with `"config unavailable"` when effective-config retrieval throws (lets the `raven` check carry the Unhealthy verdict, no double-counting). +2 unit cases pin disabled-provider skipping + the config-unavailable failure mode. |
| S3 audit fold | `86a1f40` | Codex MAJOR REVISION REQUIRED fold: M1 — `AuditLogger` no longer swallows `ConfigureExpirationOperation` failures. The bundle-enable logic moved into `IAuditRetentionConfigurator` so the rethrow-and-re-arm contract is unit-testable; failure surfaces as `RecordAsync == false` and the broadcast fail-stops with `audit_write_failed`. L1 — `MockAdminClient.broadcastRaw` now prepends a freshly-stamped audit entry matching the backend wire shape so the documented mock-mode smoke is a real round-trip. |
| S3 | `b12fb2f` | `AuditLogEntry` (`@expires +365d`, `UpdateableKeys==EmptyKeys`); `IAuditLogger.RecordAsync` → `Task<bool>`; `BroadcastService` fail-stops with `audit_write_failed` when the record fails (zero persist + zero announce); `AdminAuditController` admin-authed read with `since` / `action` / `username` / `lastN` filters; admin-ui `/audit-log` MUI DataGrid + filter chips + JSON-context modal; 8/8 backend behavior + 5/5 store unit + 24/24 contract |
| S4 audit fold | `abad57a` | Codex MINOR REVISION REQUIRED fold: M1 — `LogsStore.resubscribe` clears `entries` BEFORE the SignalR `SubscribeToLogs` invoke. The hub flushes the ring snapshot via fire-and-forget `SendAsync` BEFORE returning, so the prior post-invoke clear wiped the just-arrived snapshot. Test rewritten as a deterministic snapshot-mid-invoke pin. |
| S4 | `1db7948` | `LogStreamBuffer` (1000-entry ring, drop-oldest, fail-soft fan-out); `LogStreamProvider` MEL `ILoggerProvider` (broader surface than a Serilog sink because the project doesn't wire `UseSerilog`); `LogSanitizer` (regex set ported from wave-A1 admin-ui paste box) runs at emit time; `LogStreamHub` admin-only at `/ws/logs` with min-level + category-substring filters; admin-ui `LogsPage` rewritten as live tail with the wave-A1 sanitizer kept client-side as defence-in-depth; 7 sanitizer + 5 buffer + 5 store + 8 retargeted client-sanitizer unit tests |
| S5 audit fold | `d6bc356` | Codex MAJOR REVISION REQUIRED fold: M1 — `MigrateFromRavenAsync` no longer short-circuits on `File.Exists`; the probe keys on the Raven document instead, so a prior partial migration that left the Raven copy alive is cleaned up on every subsequent startup. File payload stays authoritative when present; otherwise the Raven content seeds the file before the Raven delete. L1 — CI backend job installs both `8.0.x` (RavenTestDriver embedded server) and `9.0.x` (test SDK) so the migration `SkippableFact`s actually run remotely. |
| S5 | `a087036` | `SecretsFileStore` (chmod-600 atomic-write JSON) replaces the Raven-backed `RealtimeSourcePolicyOverrideStore` (deleted); one-shot `MigrateFromRavenAsync` in `Startup.InitializeDatabase` is fail-stop; `appsettings.Production.json` lands with `${ENV_VAR}` placeholders; `compose.yml` mounts `consigliere-secrets` volume + `Consigliere__Secrets__Dir`; `scripts/secrets-lint.sh` + new CI job grep-gate appsettings against `"ApiKey\|Password\|Secret\|Token"` plaintext; 6 unit + 3 RavenTestDriver migration tests; runbook section |
| S6 audit fold | `1aa3089` | Codex APPROVE WITH CHANGES fold: L1 — `RequiredFromNrtFilterTests` now pin BOTH halves of the filter contract (`required` membership AND `Nullable = false` flip). Test fixtures seed `Nullable = true` so the assertion proves the filter actively flipped the flag; nullable-sibling preservation + nullable-`[Required]` non-coercion + unmatched-property non-tightening also pinned. Closes the audit's "silent regression on the openapi-typescript half" risk. |
| S6 | `a36e2a9` | `RequiredFromNrtFilter` `ISchemaFilter` populates `required` + flips `nullable: false` for NRT-NotNull properties; registered in `AddSwaggerGen`; S3 audit-log DTOs migrated under `#nullable enable` as the first concrete example + re-exported from `api.generated.ts` in `types/admin.ts`. RESIDUAL: wholesale hand-mirrored-interface sweep across remaining Dto.cs files logged for wave-A4 (the project's `Nullable=disable` setting requires per-file opt-in + per-property review). 5 NRT-filter unit cases; `pnpm verify` + `pnpm test:contract` green |
| S7 audit fold | `dfb45f6` | Codex MAJOR REVISION REQUIRED fold: H1 — `compose.prod.yml` now overrides the `consigliere` service env (`ASPNETCORE_ENVIRONMENT=Production` + `RavenDb__Urls__0` + `RavenDb__DbName` + `Consigliere__Secrets__Dir`), so the runbook's §1.5 bring-up actually loads `appsettings.Production.json` instead of the E2E overlay. `appsettings.Production.json` drops bogus `${RAVENDB_URL}` placeholders. M1 — runbook §1.4 drops the fictional `RAVEN_PASSWORD` secret + §7.3 corrects the rotation procedure to reflect the actual unsecured-private-network posture. L1 — `00-design-brief.md` no longer references the deleted `03-prod-runbook.md` stub. |
| S7 | `91562bb` | `docs/runbook.md` operator-facing handbook (8 sections + 3 appendices, angle-bracket placeholder convention, no slice/wave references in body); README Ops link; design-handoff `03-prod-runbook.md` stub removed + handoff README entry rewritten; wave A1 + A2 closeouts gain "Ops source of truth" section pointing at the runbook |
| S3 audit-2 fold | `70d847c` | Wave-closeout re-audit (fresh-model, full-range) fold: cross-slice Medium — `BroadcastAsync` hardcoded `source: "admin-ui"` + unconditional fail-stop across its three callers (admin-UI/api via `TransactionController`, wallet via `WalletHub`, background `UnconfirmedTransactionsMonitor`). New `BroadcastSource` enum threaded as a required param; audit context stamps honest provenance; fail-stop now operator-only so wallet/api/system are best-effort (unblocks the Wave-5 background retry loop on Raven-audit outage). Shape test → 4-param canonical; +2 behavior pins (per-caller source, non-operator-proceeds); admin-ui mock vocabulary `admin-ui`→`operator`. 4 Low re-audit items (S0 stale comment, S0/S1 XFF-trust knob, S2 anonymous probe amplification, S5 tmp-file mode window) logged as residuals. See `audits/S3-slice-audit-2-followup.md`. |
