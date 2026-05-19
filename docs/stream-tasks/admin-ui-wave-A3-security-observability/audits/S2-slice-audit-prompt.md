# wave-A3 S2 — slice-audit prompt

Audit target: wave-A3 S2 (Health endpoints) on
`codex/consigliere-vnext`. Diff range: `1fcd779..<S2 commit>`.

---

You are auditing the **third slice landing in wave-A3**.
S2 ships three anonymous health endpoints designed for k8s
/ Railway / any orchestrator that ships HTTP probes,
without coupling them to the admin auth scheme.

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S2 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S2 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/launch-prompt.md`
  (wave constraints: health endpoints are ANONYMOUS,
  expose only binary up/down + failing-check names)

Cross-validate against the deliverable:

- `src/Dxs.Consigliere/Health/RavenHealthCheck.cs` (new)
  — `ready`-tagged probe. `GetBuildNumberOperation` with a
  2-second timeout. Description "raven not reachable" /
  "raven not reachable (timeout)" — no hostnames / ports /
  stack traces. Exception in inner catch is swallowed by
  design.
- `src/Dxs.Consigliere/Health/ProviderReachabilityCheck.cs`
  (new) — `ready`-tagged probe. HEAD requests against the
  configured Bitails / WoC / JungleBus base URLs with a
  5-second per-target timeout. Verdict matrix:
    * all reachable → Healthy
    * ≥1 reachable → Degraded (lists the down sources)
    * 0 reachable → Unhealthy
    * 0 configured → Healthy ("no providers configured")
  Reachability is loose: ANY HTTP response with status <
  500 counts as up; only connection failure / 5xx / timeout
  counts as down. Description never exposes URLs.
- `src/Dxs.Consigliere/Health/StartupDiCheck.cs` (new) —
  `startup`-tagged probe. Resolves `IBroadcastService`; if
  that throws, returns Unhealthy with "di graph not
  resolved". Per the slice contract: must NOT block on
  Raven (the resolve is non-network).
- `src/Dxs.Consigliere/Health/HealthResponseWriter.cs`
  (new, internal) — single JSON shape:
  `{status, checks: [{name, status, description, durationMs}]}`.
  All three endpoints share it.
- `src/Dxs.Consigliere/Setup/HealthChecksSetup.cs` (new) —
  `AddConsigliereHealthChecks` registers a named
  HttpClient + three named checks with their tags;
  `MapConsigliereHealthEndpoints` maps the three paths
  with `Predicate = _ => false` / `Predicate = c =>
  c.Tags.Contains("ready"|"startup")`. Each route also
  calls `.AllowAnonymous()`.
- `src/Dxs.Consigliere/Startup.cs` — services chain calls
  `AddConsigliereHealthChecks` between
  `AddConsigliereForwardedHeaders` and the zone services;
  endpoint chain calls `MapConsigliereHealthEndpoints`
  before the controller routes + fallback so the index.html
  fallback doesn't swallow them.
- `infrastructure/caddy/Caddyfile.prod` — adds
  `@health path /health/*` + `log_skip @health` so probe
  noise doesn't drown out the access log. Holds the
  matcher where S1's rate-limit bypass will land.
- `tests/Dxs.Consigliere.Tests/Health/
  HealthChecksSetupTests.cs` (new) — 3 unit tests pinning
  the tag registration matrix + the live predicate's
  empty-checks contract + the named HttpClient.
- `tests/Dxs.Consigliere.Tests/Health/
  ProviderReachabilityCheckTests.cs` (new) — 6 unit tests
  exercising the verdict matrix via a stub
  HttpMessageHandler. No network egress.
- `src/admin-ui/tests/contract/health.test.ts` (new) — 4
  vitest describes hitting the three endpoints anonymously
  against the real ASP.NET host + real Raven (the
  wave-A2 contract host harness). Pins the JSON shape +
  the per-endpoint check list.
- `docs/runbook.md` — new "Health probes" section: the
  table, a sample k8s probe block, the Caddy bypass note,
  the JSON shape.

---

## What's in scope for this audit

1. **Anonymous probes.** All three paths must reach the
   client without an `Authorization` header / cookie. The
   integration test in `health.test.ts` mints a fresh
   anonymous HostHandle per spec for belt-and-braces.
2. **No information leakage.** Descriptions MUST NOT
   contain URLs, secrets, hostnames, or stack traces.
   Re-read every `HealthCheckResult.Unhealthy(...)` /
   `Degraded(...)` call site.
3. **Live ≠ ready.** `/health/live` MUST return 200 even
   when Raven is unreachable. Audit the live endpoint's
   predicate (`_ => false`) + the unit test that locks it.
4. **Startup probe is non-blocking on Raven.** Resolving
   `IBroadcastService` from the container does not connect
   to Raven; the slice contract relies on this. If the
   reviewer believes otherwise, walk the dependency tree
   in `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs`.
5. **No regression of the wave-A2 contract suite.**
   `pnpm test:contract` must run 20/20 green (was 16; +4
   health describes).
6. **Caddy bypass shape.** `Caddyfile.prod` `@health path
   /health/*` matcher + `log_skip` must NOT change request
   forwarding semantics — the upstream still sees the
   request. Reviewer should confirm the matcher precedes
   any later rate-limit / WAF block that S1 adds.

## Verdict + finding format

Verdict line first:
- `APPROVE`
- `APPROVE WITH CHANGES` — minor (L*) findings only
- `MAJOR REVISION REQUIRED` — at least one C/H/M

Findings tagged `C* | H* | M* | L*` (Critical / High /
Medium / Low). Each finding contains:
- file:line of the defect
- why it matters (security / correctness consequence)
- recommended fix (specific, not "consider re-architecting")

Out of scope for this audit (later slices in the wave):
S1 rate limiting (the Caddy `@health` matcher is set up
for it but the bypass itself is S1), S3 audit log, S4 log
stream, S5 secrets, S6 NRT, S7 runbook completion.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere.
- `dotnet test --filter Health.HealthChecksSetupTests|Health.ProviderReachabilityCheckTests`
  9/9 green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`
  20/20 green (the new `health.test.ts` adds 4 specs;
  total goes from 16 → 20).
- `pnpm verify` green.
- `docker compose --profile dev up -d --build` then
  `curl -sS http://localhost:5000/health/live` (skipping
  Caddy to exercise Kestrel directly) returns the JSON
  shape with `status: "healthy"` + empty checks.
- (Manual) stop Raven, hit `/health/ready` via the dev
  Caddy: expect 503 with the JSON body listing raven as
  unhealthy. Bring Raven back: expect 200.
