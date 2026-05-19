---
created: 2026-05-19
type: wave-slices
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md
---

# Wave A3 — Slice decomposition

## Overview

Eight slices. S0..S5 + S7 are mostly independent; S6
depends on wave-A2 S1 (codegen pipeline) but is otherwise
self-contained. Recommended order: S0 → S2 → S1 → S5 →
S3 → S4 → S6 → S7. S0 first because TLS configuration
needs to be in place before rate limiting can correctly
attribute requests to client IPs (via the
`X-Forwarded-For` header that Caddy adds).

Each slice ships via the wave-A1/A2 cadence: prompt →
execute → audit → fold → commit. Audit prompts live at
`audits/S<n>-slice-audit-prompt.md`.

---

## S0 — TLS termination + cookie Secure flag

### Intent
Move TLS termination to a Caddy reverse proxy in compose
so the application code stops touching certificate
material. Cookies flip to `Secure` everywhere.

### Owned paths
- `compose.yml` — add a Caddy service depending on
  `consigliere` + `ravendb`
- `infrastructure/caddy/Caddyfile.prod` — Let's Encrypt
  with the operator's domain as a `${CADDY_DOMAIN}` env
- `infrastructure/caddy/Caddyfile.dev` — internal TLS
  with the bundled Caddy self-signed CA
- `infrastructure/caddy/snippets/headers.caddy` — HSTS +
  CSP scaffolding
- `src/Dxs.Consigliere/Setup/AdminAuthSetup.cs` — cookie
  options bind from `Cookie:Secure` config (Always /
  SameAsRequest)
- `src/Dxs.Consigliere/Startup.cs` — enable
  `ForwardedHeadersOptions` so Kestrel honours
  `X-Forwarded-For` + `X-Forwarded-Proto`
- `src/Dxs.Consigliere/appsettings.Production.json` (new
  in S5; this slice creates the cookie-section skeleton
  if the file exists)
- `docs/runbook.md` (just the TLS section now; S7
  consolidates)

### Exact task
1. Add Caddy 2 service to `compose.yml`:
   - Image `caddy:2-alpine`
   - Bind-mount `infrastructure/caddy/Caddyfile.${PROFILE}`
     to `/etc/caddy/Caddyfile`
   - Bind-mount `caddy-data` volume for Let's Encrypt
     persistence
   - Exposes :80 + :443 publicly; reverse-proxies to
     `consigliere:5000`
2. Write the two Caddyfiles. Prod uses `tls
   ${CADDY_EMAIL}` + ACME; dev uses `tls internal`.
   Both apply the HSTS snippet.
3. Inside Consigliere:
   - Add `UseForwardedHeaders(new ForwardedHeadersOptions
     { ForwardedHeaders =
     ForwardedHeaders.XForwardedFor |
     ForwardedHeaders.XForwardedProto })` BEFORE
     `UseRouting` in Startup.cs.
   - Flip cookie `SecurePolicy` to `Always` when bound
     config `Cookie:Secure = "Always"`. Default = `Always`
     in `appsettings.Production.json`; dev can override
     to `SameAsRequest`.
4. Smoke: `docker compose up --build` with the dev
   Caddyfile. `curl -k https://localhost/health/live`
   returns 200 (the endpoint lands in S2, so use the
   wave-A2 setup-status route here for the smoke).

### What not to do
- Don't terminate TLS inside Kestrel. Certificate hot-
  swap, OCSP, ACME — all owned by Caddy.
- Don't ship Caddy bundled inside the Consigliere
  container. Separate service.
- Don't make the cookie unconditionally `Secure` in dev;
  it breaks the http://localhost flow used by Playwright
  (already configured `dev` profile relies on plain HTTP
  via mock mode).

### Validation
- `docker compose up --build` exposes :443 only (port
  5000 only inside the docker network)
- `curl -kI https://localhost/` returns
  `strict-transport-security: max-age=...`
- After login via the proxied URL, the
  `consigliere_admin` cookie has `Secure; HttpOnly;
  SameSite=Lax` flags
- New unit test in `tests/Dxs.Consigliere.Tests/` pins
  `ForwardedHeaders` is enabled

### Completion signal
First-run install instructions in the runbook stop
mentioning `http://` for production.

---

## S1 — Rate limiting on auth + broadcast

### Intent
A naive brute-force attempt against `/api/admin/auth/
login` fails after 5 attempts/minute; the destructive
broadcast endpoint is throttled to 1/second so a
runaway client can't DoS the backend.

### Owned paths
- `src/Dxs.Consigliere/Setup/RateLimiterPolicies.cs`
  (new) — policy definitions via the
  `Microsoft.AspNetCore.RateLimiting` builder
- `src/Dxs.Consigliere/Setup/PublicApiSetup.cs` —
  registers the middleware
- `src/Dxs.Consigliere/Controllers/AdminAuthController.cs`
  — `[EnableRateLimiting("login")]` + `[EnableRateLimiting
  ("me")]`
- `src/Dxs.Consigliere/Controllers/TransactionController.
  cs` — `[EnableRateLimiting("broadcast")]` on POST
  `/broadcast`
- `src/Dxs.Consigliere/Configs/RateLimitingConfig.cs`
  (new) — bound from `RateLimiting:*` config
- `tests/Dxs.Consigliere.Tests/RateLimiting/*` — new
  integration test class
- `src/admin-ui/src/lib/api/client.ts` — handle 429 with
  exponential backoff (one retry only)

### Exact task
1. Add the rate-limiter middleware:
   ```csharp
   services.AddRateLimiter(opts => {
     opts.RejectionStatusCode = 429;
     opts.OnRejected = AddRetryAfterHeader;
     opts.AddPolicy("login", ipBucket(perMin: 5));
     opts.AddPolicy("me", ipBucket(perMin: 100));
     opts.AddPolicy("broadcast", ipBucket(perSec: 1));
   });
   ```
2. Attribute the controller methods. `login` policy on
   POST /login, `me` policy on GET /me, `broadcast` on
   POST /tx/broadcast.
3. Frontend `ApiClient`: when the server returns 429,
   read `Retry-After` (seconds), sleep that long, retry
   ONCE. Subsequent 429 surfaces as `AppError {
   category: "RateLimited" }`.
4. Integration test: spin a TestServer (or use the
   wave-A2 host harness directly), fire 10 login
   attempts in <1s, assert 5×200/302 + 5×429.

### What not to do
- Don't rate-limit health endpoints. Orchestrator probes
  hammer them on purpose.
- Don't share the same policy across auth + broadcast —
  they have different threat models + different limit
  ergonomics.
- Don't write a custom in-memory token bucket. The .NET
  built-in is battle-tested.
- Don't store rate-limit counters in Raven. Per-process
  is fine; multi-instance HA is wave-A4.

### Validation
- New integration test passes
- Manual smoke: `for i in $(seq 1 10); do curl -i -X
  POST -d '...' https://localhost/api/admin/auth/login;
  done` — 5 first ones return 200/302, next 5 return
  429 with `Retry-After: 60`
- Frontend `ApiClient` correctly surfaces
  `RateLimited` AppError category

### Completion signal
A brute-force script (test fixture) trying to guess the
admin password takes >24h to make 10000 attempts at the
new limits.

---

## S2 — Health endpoints

### Intent
Orchestrators can ask "are you up?" without a session.

### Owned paths
- `src/Dxs.Consigliere/Setup/HealthChecksSetup.cs` (new)
- `src/Dxs.Consigliere/Startup.cs` — wires
  `app.MapHealthChecks(...)`
- `src/Dxs.Consigliere/Health/RavenHealthCheck.cs` (new)
- `src/Dxs.Consigliere/Health/ProviderReachabilityCheck.
  cs` (new) — Bitails + WhatsOnChain + JungleBus
- `infrastructure/caddy/Caddyfile.prod` — bypass
  rate-limit + log noise reduction for `/health/*`
- `docs/runbook.md` (health section)

### Exact task
1. Add `Microsoft.Extensions.Diagnostics.HealthChecks`
   (already a transitive dep — pin it explicitly).
2. Register three `MapHealthChecks` paths with tag
   predicates:
   - `/health/live` — predicate `_ => false` (no
     checks, just a 200 if the process listens)
   - `/health/ready` — predicate `c => c.Tags.
     Contains("ready")`
   - `/health/startup` — predicate `c => c.Tags.
     Contains("startup")`
3. Implement health checks:
   - `RavenHealthCheck` — `DocumentStore.Maintenance.
     Server.SendAsync(new GetBuildNumberOperation())`
     with a 2s timeout; `ready` tag.
   - `ProviderReachabilityCheck` — HEAD requests
     against each configured provider with a 5s
     timeout; `ready` tag; degraded (not unhealthy) if
     only one provider is down.
   - DI graph resolution probe — explicit
     `IServiceProvider.GetRequiredService<RootStore>()`-
     equivalent (we don't have a RootStore on the
     backend; check that BroadcastService + key singletons
     resolve); `startup` tag.
4. JSON response writer that emits `{status, checks: [
   {name, status, description, duration} ]}` so the UI
   can render it later (wave-A4 surfaces this in the UI).

### What not to do
- Don't return sensitive details in `description`. "raven
  not reachable" not "Raven at http://ravendb:8080 returned
  500: <secret error stack>".
- Don't use the same check for live + ready. `live` must
  ALWAYS return 200 if the process is up — that's how
  orchestrators tell "restart me" from "give me a
  second."
- Don't make startup checks blocking on Raven. The host
  has its own retry loop in `Program.cs`; startup probe
  reports "still starting up" while that's running.

### Validation
- Anonymous `curl http://localhost:5000/health/live` →
  200 even when Raven is down (probe behind Caddy
  works after S0 lands)
- `curl /health/ready` → 503 with the bad-check list
  when Raven is stopped, 200 when it comes back
- Integration test pins each endpoint's tag predicates

### Completion signal
A k8s deploy YAML using the three probes Just Works
against the dev image.

---

## S3 — Audit log for destructive operations

### Intent
Force-rebroadcast (and future destructive operations)
leave a permanent, queryable record of WHO did WHAT,
WHEN, against WHICH resource.

### Owned paths
- `src/Dxs.Consigliere/Data/Models/Audit/AuditLogEntry.
  cs` (new)
- `src/Dxs.Consigliere/Services/Audit/IAuditLogger.cs`
  (new)
- `src/Dxs.Consigliere/Services/Audit/AuditLogger.cs`
  (new)
- `src/Dxs.Consigliere/Controllers/AdminAuditController.
  cs` (new) — `GET /api/admin/audit-log?since=&type=&
  user=&lastN=`
- `src/Dxs.Consigliere/Dto/Responses/Admin/AdminAuditLog
  Response.cs` (new)
- `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs`
  — inject `IAuditLogger`, call `RecordAsync` BEFORE
  the broadcast
- `src/admin-ui/src/lib/admin/admin-client.ts` —
  `getAuditLog(opts)`
- `src/admin-ui/src/screens/audit-log/AuditLogPage.tsx`
  + `audit-log.store.ts` + tests (new)
- `src/admin-ui/src/types/admin.ts` — hand-mirrored
  AdminAuditLogResponse (S6 swaps to generated)
- `src/admin-ui/src/app/App.tsx` — new authed route
  `/audit-log`
- `src/admin-ui/src/app/routes.ts` — drawer entry
  under the System section

### Exact task
1. `AuditLogEntry` model:
   ```csharp
   public sealed class AuditLogEntry {
     public string Id { get; set; }
     public long UnixMs { get; set; }
     public string Username { get; set; }
     public string Action { get; set; }  // "broadcast_tx", "config_update", ...
     public string TargetId { get; set; }  // txid, address, providerId
     public string Context { get; set; }  // JSON blob, opaque
   }
   ```
   Apply Raven `@expires` metadata = `DateTime.UtcNow.
   AddDays(365)`.
2. `IAuditLogger.RecordAsync(string action, string
   targetId, object context)` opens a Raven session,
   stores the entry, saves. Returns Task<bool> so the
   caller can fail-stop on write failure.
3. `BroadcastService` change: BEFORE the network send,
   call `_auditLogger.RecordAsync("broadcast_tx",
   txid, new { rawHexLength, source: "admin-ui" })`.
   If the write fails, throw `OperationCanceledException`
   wrapped in `BroadcastException("audit_write_failed")`.
4. Admin REST: `GET /api/admin/audit-log` paginated,
   filterable by `since` (unix ms), `action`, `username`,
   `lastN`. Same `ClampLastN` pattern as wave-A1's
   alerts endpoint.
5. Admin UI: new `audit-log` route, MUI DataGrid with
   columns (UTC time, user, action, target, context
   button — opens dialog with JSON), filter chips.
6. Contract describe + e2e: an e2e flow that confirms
   Force-rebroadcast in the dialog AND then visits
   `/audit-log` finds the new entry.

### What not to do
- Don't compute or expose hashes of `rawHex` in
  `Context` — that leaks the broadcast content. Store
  ONLY `{ rawHexLength, source }`.
- Don't make the audit log mutable from any UI surface.
  Read-only forever.
- Don't co-locate audit reads with other admin reads in
  the same store/screen. Audit has different access
  semantics (some future RBAC role might see audit and
  nothing else).

### Validation
- New contract describe in `tests/contract/admin.test.
  ts` asserts the AdminAuditLogResponse shape
- New e2e spec (`tests/e2e/audit-log.spec.ts`) walks:
  login → /broadcast-queue → force rebroadcast confirm
  → /audit-log → finds the entry
- Unit test for `AuditLogger.RecordAsync` failure path:
  `BroadcastService` propagates the failure

### Completion signal
A forensic question like "who pressed Force-rebroadcast
on tx X on Friday?" is answerable from the UI in <30s.

---

## S4 — Backend log streaming

### Intent
Replace the wave-A1 `/logs` paste box with a real-time
live tail of the backend Serilog stream.

### Owned paths
- `src/Dxs.Consigliere/Logging/LogStreamSink.cs` (new) —
  in-process Serilog sink with a ring buffer
- `src/Dxs.Consigliere/Logging/LogEventDto.cs` (new) —
  frozen wire shape
- `src/Dxs.Consigliere/WebSockets/LogStreamHub.cs` (new)
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` —
  extend with `OnLogEvent` (or keep on a separate hub
  path `/ws/logs`)
- `src/Dxs.Consigliere/Setup/RealtimeSetup.cs` (or
  appropriate hub-wiring file) — register the new hub
- `src/admin-ui/src/lib/signalr/client.ts` —
  `subscribeToLogs(filter?)` invocation
- `src/admin-ui/src/lib/events/bus.ts` — new `OnLogEvent`
  event type
- `src/admin-ui/src/screens/logs/LogsPage.tsx` — replace
  paste box with live-tail virtual scroll (use
  `react-window` if needed for perf; default = scroll
  the last 1000 events)
- `src/admin-ui/src/screens/logs/logs.store.ts` (new) —
  subscription + filter state
- `src/admin-ui/src/screens/logs/LogsPage.test.tsx`
  (overwritten)

### Exact task
1. Serilog sink: subscribe to log events, push the last
   1000 into a `Channel<LogEventDto>` ring buffer. Drop
   oldest on overflow. Sanitize sensitive fields
   server-side (same regex set the wave-A1 paste box
   used, MOVED TO BACKEND).
2. `LogStreamHub.SubscribeToLogs(LogLevel? minLevel,
   string? categoryFilter)`: pump the buffer to the
   client. Use the wave-A1 SignalR auth model — admin
   cookie required.
3. Frontend: replace the paste box. `LogsPage` mounts,
   creates `LogsStore`, calls
   `signalR.subscribeToLogs(...)`. Bus event handler
   appends to a MobX-observable circular buffer.
   Virtualized scroll for the visible rows.
4. Keep the wave-A1 sanitizer REGEXES as a defense-in-
   depth pass on the client (the server-side
   sanitizer is the primary; client is the second
   layer).

### What not to do
- Don't write log events to Raven on the hot path. The
  ring buffer is in-memory by design — overwhelmed
  Serilog hot paths must not block.
- Don't expose unsanitized logs in the wire payload.
  Sanitize at the sink, not on the wire.
- Don't make the SignalR subscription persistent across
  reconnects (the buffer is local to the process — if
  the backend restarts, the client buffer is wiped on
  reconnect, which is correct).

### Validation
- E2e spec: navigate to `/logs`, send a sample log via
  a backend test endpoint (or trigger an existing
  event), see it appear in the virtual list
- Server-side sanitizer unit test: every regex from
  the wave-A1 set fires red on the appropriate
  test fixture

### Completion signal
The admin-ui `/logs` paste box is gone; live tail is
the only surface.

---

## S5 — Secrets at rest

### Intent
No plaintext secrets in git, in process env, or in
RavenDB documents.

### Owned paths
- `src/Dxs.Consigliere/appsettings.Production.json`
  (new)
- `src/Dxs.Consigliere/Data/SecretsFileStore.cs` (new)
  — wraps `data/secrets/providers.json` reads + writes
  with chmod 600
- `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs` —
  bind provider config from `SecretsFileStore` when the
  file exists; fall back to Raven document otherwise
  (back-compat for in-flight wave-A2 installs only —
  see migration rules below)
- `src/Dxs.Consigliere/Services/Impl/SetupWizardService.
  cs` (or equivalent) — submit path writes to the file
  + REMOVES the previously-Raven-side document
- `compose.yml` — Docker secrets binding for the
  `data/secrets/` mount
- `infrastructure/caddy/Caddyfile.prod` — references
  `${CADDY_DOMAIN}` + `${CADDY_EMAIL}` from env (not
  inline strings)
- `.github/workflows/ci-tests.yml` — new
  `secrets-lint` step that greps `appsettings.*.json`
  for plaintext-looking values
- `docs/runbook.md` (secrets rotation section)

### Migration
- A wave-A2-installed system has a Raven doc
  `provider-config/effective` with plaintext API keys.
  On first wave-A3 startup, `SecretsFileStore.Migrate(
  raven)` reads that doc, writes to
  `data/secrets/providers.json`, then DELETES the Raven
  doc.
- After the migrate fires once, the Raven path is dead.
  Future config writes go via the file store ONLY.
- Migration is fail-stop: if the file can't be written
  (disk full, perms), the host refuses to start.

### What not to do
- Don't keep the Raven path "for legacy compatibility."
  Both ends of the consumer chain are in-repo — wave-A2
  S0 wrote it, S5 reads it, S5 owns the cutover.
- Don't store the file as 644. Chmod 600 (owner-rw
  only). Docker secrets are mounted 444 by default;
  acceptable.
- Don't generate the file outside the setup wizard. The
  wizard is the one writer.

### Validation
- `grep -E '"(ApiKey|Password)"\s*:\s*"[^${]' src/Dxs.
  Consigliere/appsettings.*.json` → empty
- CI `secrets-lint` step fails red on `appsettings.json`
  edits that re-introduce plaintext values
- Compose-fresh install boots with `SECRETS_DIR` env
  pointing at a Docker secret mount
- Migration test: seed a Raven doc, start the host,
  assert the file exists + the Raven doc is gone

### Completion signal
`git grep "api[_-]?key.*\":\\s*\"[A-Za-z0-9]" -- src/
Dxs.Consigliere/appsettings*` returns ZERO matches.

---

## S6 — Swashbuckle NRT inference + screen migration

### Intent
The wave-A2 generated `api.generated.ts` becomes the
SOURCE for every screen-side wire DTO. Hand-mirrored
interfaces under `types/admin.ts` + `types/auth.ts`
become re-exports.

### Owned paths
- `src/Dxs.Consigliere/Swagger/RequiredFromNrtFilter.cs`
  (new) — `ISchemaFilter` that walks each property's
  nullability + populates the schema's `required` array
- `src/Dxs.Consigliere/Setup/PublicApiSetup.cs` —
  registers the filter in `AddSwaggerGen(...)`
- `src/admin-ui/contracts/swagger.json` — regenerated
- `src/admin-ui/src/types/api.generated.ts` —
  regenerated (now with proper `required` arrays = no
  `?:` on non-nullable C# properties)
- `src/admin-ui/src/types/admin.ts` — every wire DTO
  removed, replaced with `export type X = components[
  "schemas"]["X"];` re-exports
- `src/admin-ui/src/types/auth.ts` — same
- Screen-side imports unchanged (still import from
  `@/types/admin`); the re-export bridge keeps the
  cutover behaviorally inert
- Audit fold: any screen that mis-handles
  `nullable: false` properties picks up a TS error

### Exact task
1. Swashbuckle filter:
   ```csharp
   public sealed class RequiredFromNrtFilter : ISchemaFilter {
     public void Apply(OpenApiSchema schema, SchemaFilterContext ctx) {
       if (schema.Type != "object" || schema.Properties is null) return;
       var required = new HashSet<string>(schema.Required ?? new());
       foreach (var (jsonName, openApiProperty) in schema.Properties) {
         var clrProperty = ctx.Type.GetProperties().FirstOrDefault(p => string.Equals(p.Name, jsonName, StringComparison.OrdinalIgnoreCase));
         if (clrProperty is null) continue;
         var nrtContext = new NullabilityInfoContext();
         var info = nrtContext.Create(clrProperty);
         if (info.WriteState == NullabilityState.NotNull) required.Add(jsonName);
       }
       schema.Required = required;
     }
   }
   ```
2. Register: `services.AddSwaggerGen(c => c.SchemaFilter
   <RequiredFromNrtFilter>())`.
3. `pnpm contracts:generate` — see fewer `?:` properties.
4. Update `types/admin.ts` + `types/auth.ts`:
   - Drop the hand-mirrored interface bodies for every
     wire DTO that now has a generated equivalent
   - Re-export each as a `type` alias from
     `api.generated.ts`
   - Keep hand-mirrored helpers (`TX_HAPPY_PATH`,
     `OutgoingTxState` union literal, etc.) — those
     aren't wire shapes
5. `pnpm verify` — fix any newly-surfaced TS errors.
   Most should be sites that destructured an optional
   field without a null check; the type now says it's
   required, but the screen treated it as optional.

### What not to do
- Don't sprinkle `[Required]` attributes across C# DTOs.
  NRT-driven is the right channel.
- Don't fix Compile errors by reverting the new types.
  Fix the screen-side handling instead.
- Don't migrate types files for DTOs that don't have a
  generated equivalent. E.g. `OutgoingTxState` union is
  a backend ENUM Swashbuckle emits as a string schema;
  the TS union literal is the right helper.

### Validation
- Generated file diff: fewer `?:` properties, more
  `required: [...]` arrays
- `pnpm verify` clean
- `pnpm test:contract` still 12/12 green (the gate
  doesn't care which side the types live on)
- New unit test for `RequiredFromNrtFilter`: feed a DTO
  with mixed nullable + non-nullable props, assert the
  emitted schema's `required` array

### Completion signal
`grep -rn "interface Admin\|interface P2p\|interface
Source\|interface Setup" src/admin-ui/src/types/{admin,
auth}.ts` returns ZERO matches — every wire DTO is a
re-export.

---

## S7 — Operator runbook

### Intent
A non-engineer (the customer's SRE / DevOps person)
can stand up Consigliere, monitor it, recover from
outages, rotate secrets, restore from backup — without
reading source code.

### Owned paths
- `docs/runbook.md` (new) — the deliverable
- `README.md` — link to the runbook in the Ops section
- `docs/admin-ui/design-handoff/03-prod-runbook.md` —
  REPLACED by `docs/runbook.md`; remove the stub
- Wave A1 + A2 closeout docs — update the "ops source
  of truth" links

### Exact task

`docs/runbook.md` has 8 sections (each a numbered
checklist, each command copy-pasteable):

1. **First-time deployment** — on a fresh Ubuntu/Debian
   VM, install docker + docker-compose, clone repo,
   create `.env` with the four secrets (CADDY_DOMAIN,
   CADDY_EMAIL, RAVEN_PASSWORD, JWT_SIGNING_KEY),
   `docker compose --profile prod up -d --build`.
2. **First-run setup wizard** — visit
   `https://<domain>/setup`, complete the four-step
   wizard from wave-A2 S0. Includes screenshots.
3. **Scaling guidance** — vertical first (4 CPUs / 8GB
   covers a single SP node); horizontal deferred to
   wave-A4 multi-instance.
4. **Monitoring + alerting** — what to scrape from
   `/health/ready`, how to wire it to PagerDuty/
   Opsgenie/etc., recommended thresholds.
5. **Log streaming** — how to use `/logs` for live
   investigation; how to ship Serilog to an external
   sink (Loki/Datadog) by adding a sink in
   `appsettings.Production.json`.
6. **Audit log forensics** — sample queries against the
   `/audit-log` screen, retention rules, export
   procedure.
7. **Secrets rotation** — admin password (re-run the
   setup wizard with the same username), provider API
   keys (edit `data/secrets/providers.json` + send
   SIGHUP to the host, OR restart the compose service).
8. **Disaster recovery** — Raven backup setup (off-host
   via `Smuggler.exe` cron), restore procedure, RTO
   target ~30 min on the wave-A3 stack.

### What not to do
- Don't reference internal slice / wave names in the
  runbook. The customer doesn't care which wave shipped
  the feature.
- Don't write a TROUBLESHOOTING section with vague "if
  you see X, try Y" entries. Concrete procedures only.
  Anything that needs developer judgment goes in an
  `escalate to engineering` step.
- Don't include sample data with real-looking
  addresses, txids, or domain names. Use `<placeholder>`
  + a documented set of fake values.

### Validation
- A non-engineer (e.g. a fresh person from the team or
  the user themselves) reads ONLY the runbook + stands
  up the stack from a clean VM. Stopwatch < 45 min for
  the first-time install.
- Every command in the runbook is `bash -x`-friendly:
  no missing env vars, no implicit cwd assumptions.

### Completion signal
The runbook is the artifact a future operator can read
in lieu of reading source. The wave closeout doc
defers to it for ops topics.

---

## Dependency Order

```
S0 (TLS + cookie Secure)
   ↓
S2 (health) ─→ S1 (rate limit)
   ↓                ↓
S5 (secrets at rest)
   ↓
S3 (audit log) ─→ S4 (log streaming)
   ↓
S6 (NRT inference + screen migration)
   ↓
S7 (runbook)
```

- S0 first: TLS termination + Forwarded headers + cookie
  Secure are foundational.
- S2 and S1 can ship in either order; S2 first preferred
  because it's lower-risk + unblocks orchestrator
  staging.
- S5 needs S0 (Caddy ENV plumbing); ideally lands before
  S3 so audit entries don't accidentally reference
  Raven-stored plaintext.
- S3 → S4 because they share the SignalR hub auth
  pattern.
- S6 last among feature slices because it forces a
  screen-side TS sweep; safer after S3 (audit-log
  screen exists + needs its types regenerated).
- S7 last because it codifies what shipped.

## Validation Matrix

| slice | local validation | CI gate |
|---|---|---|
| S0 | `docker compose --profile dev up --build` + HSTS curl smoke | new step that runs the compose stack + curls the health endpoint via Caddy |
| S1 | 10-burst login curl + integration test | existing backend test job picks up new test class |
| S2 | curl each `/health/*` path | `dotnet test` (backend job) |
| S3 | e2e spec + force-rebroadcast smoke against the audit log | existing admin-ui-e2e job |
| S4 | live-tail e2e + sanitizer unit tests | existing admin-ui-e2e + admin-ui jobs |
| S5 | grep gate + migration unit test | new CI step + backend test job |
| S6 | `pnpm verify` + `pnpm test:contract` | existing admin-ui + admin-ui-contracts jobs |
| S7 | manual stopwatch read-through | manual review checkbox in closeout |

End-to-end: a fresh `git clone` + `docker compose
--profile prod up --build` + reading the runbook puts
a new operator at a working, audit-trail-equipped,
TLS-fronted Consigliere in < 45 minutes.

## Closeout Requirements

- 8 slices `done`; each has an
  `audits/S<n>-slice-audit-followup.md` recording
  verdict + folded findings.
- `audits/A1.md` summarises closeout — what landed,
  validation evidence, residuals.
- `evidence/closeout.md` follows the wave-A2 template
  (scope · sources of truth · validation commands · CI
  evidence · per-slice delivery hashes · contract
  parity evidence · mobile/a11y/perf reports ·
  accepted design deviations · residuals · open
  assumptions).
- Update wave-A2 + wave-A1 closeout docs: tick the four
  closed residuals (chicken-and-egg first-run, NRT
  required-field inference, screen migration onto
  generated types, backend log streaming endpoint).
- `docs/runbook.md` reviewed + accepted by the
  human operator running the smoke deploy.
