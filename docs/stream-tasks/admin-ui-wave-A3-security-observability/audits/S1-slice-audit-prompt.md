# wave-A3 S1 — slice-audit prompt

Audit target: wave-A3 S1 (Rate limiting on auth + broadcast)
on `codex/consigliere-vnext`. Diff range: `1ce4d24..<S1 commit>`.

---

You are auditing the **fourth slice in wave-A3**. S1 plugs
the `Microsoft.AspNetCore.RateLimiting` middleware into
Consigliere's load-bearing endpoints (login, me, broadcast)
and wires the admin UI's `ApiClient` to honour the standard
`Retry-After` header exactly once.

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S1 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S1 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/launch-prompt.md`
  (wave constraints: conservative defaults — 5 login/min,
  100 me/min, 1 broadcast/sec; override via env; document
  in runbook)

Cross-validate against the deliverable:

- `src/Dxs.Consigliere/Configs/RateLimitingConfig.cs` (new)
  — bind shape for three named policies with conservative
  defaults; `PermitsPerSecond` takes precedence over
  `PermitsPerMinute`.
- `src/Dxs.Consigliere/Setup/RateLimiterPolicies.cs` (new)
  — `AddConsigliereRateLimiting` registers three policies
  via `services.AddRateLimiter`. IP-keyed fixed-window
  partitions, with the policy name folded into the partition
  key so /login exhaustion does not bleed into /me's
  quota. `OnRejectedAsync` writes the `Retry-After`
  header from the lease metadata.
- `src/Dxs.Consigliere/Startup.cs`:
    * `ConfigureServices` calls
      `AddConsigliereRateLimiting(configuration)` between
      `AddConsigliereHealthChecks()` and the zone services.
    * `Configure` calls `app.UseRateLimiter()` AFTER
      `UseAuthentication`/`UseAuthorization` so future
      per-user partitions can read the resolved identity,
      and BEFORE `UseEndpoints` so the
      `[EnableRateLimiting]` attributes actually fire.
- `src/Dxs.Consigliere/Setup/HealthChecksSetup.cs` —
  every `MapHealthChecks(...)` call now chains
  `.AllowAnonymous().DisableRateLimiting()`. Orchestrator
  probes must not 429 — the slice contract is explicit.
- `src/Dxs.Consigliere/Controllers/AdminAuthController.cs`
  — `[EnableRateLimiting("me")]` on `GET /me`,
  `[EnableRateLimiting("login")]` on `POST /login`. Both
  attributes name the policy via
  `RateLimiterPolicies.LoginPolicy` / `.MePolicy` constants.
- `src/Dxs.Consigliere/Controllers/TransactionController.cs`
  — `[EnableRateLimiting("broadcast")]` on
  `POST /api/tx/broadcast`.
- `src/Dxs.Consigliere/appsettings.Test.json` — loose
  overrides (10000/min for the chatty `me` policy, 10000/sec
  for broadcast) so the shared contract host doesn't trip
  the limiter mid-suite. The dedicated strict-limit host the
  S1 burst test spawns overrides these per-process.
- `tests/Dxs.Consigliere.Tests/RateLimiting/
  RateLimiterPoliciesTests.cs` (new) — 6 unit tests pin
  the per-policy permit ceiling, the partition-key
  namespacing, the unknown-IP fallback, and the
  PerSecond-wins-over-PerMinute precedence rule.
- `src/admin-ui/tests/contract/_host-harness.ts` — adds
  an `envOverrides` knob on `SpawnOptions`. Used ONLY by
  the rate-limit test file to spawn a strict-limit host;
  the shared `_global-setup` host stays untouched.
- `src/admin-ui/tests/contract/rate-limit.test.ts` (new) —
  spawns a dedicated host with
  `RateLimiting__Login__PermitsPerMinute=5`. 10-login
  burst proves the 5+5 cutoff and asserts the
  `Retry-After` header is present on every 429. Two
  follow-on describes prove /me stays on its own
  partition and `/health/live` stays opted out.
- `src/admin-ui/src/types/errors.ts` — new
  `"RateLimited"` `AppErrorCategory` variant.
- `src/admin-ui/src/lib/api/client.ts` — single-retry
  contract: 429 + `Retry-After` → sleep ≤ 65s → retry
  once; second 429 throws
  `makeAppError("RateLimited", ..., 429)`. Missing /
  malformed header → no retry, immediate
  `RateLimited` throw.
- `src/admin-ui/src/lib/api/client.test.ts` (new) — 4
  vitest cases pin the single-retry contract, the 65s
  ceiling clamp, and the missing-header path.
- `src/admin-ui/contracts/swagger.json` +
  `src/admin-ui/src/types/api.generated.ts` —
  regenerated to surface the new
  `[ProducesResponseType(429)]` annotations on login +
  broadcast.
- `docs/runbook.md` — new Rate-limiting section: policy
  table, env-var overrides, client retry contract.

---

## What's in scope for this audit

1. **Conservative defaults match the launch-prompt.** 5
   login/min, 100 me/min, 1 broadcast/sec. The Test
   appsettings + the strict-limit override host both
   exercise the binding code path so a missed key would
   surface red.
2. **Health endpoints stay opted out.** The slice contract
   forbids rate-limiting `/health/*`. Verify
   `.DisableRateLimiting()` survives on all three
   `MapHealthChecks` calls + the rate-limit contract test's
   `/health/live` burst stays 200.
3. **Per-policy partition keys.** A login burst MUST NOT
   exhaust /me. Unit test
   `Partition_keys_are_namespaced_per_policy` pins this in
   process; the contract test
   `/api/admin/auth/me is on its own partition` pins it on
   the wire.
4. **Forwarded headers feed the limiter key.** Wave-A3 S0
   already wired `UseForwardedHeaders` BEFORE
   `UseRouting`, so `Connection.RemoteIpAddress` carries
   the real client IP through the proxy hop. If the audit
   suspects a regression, the strict-limit contract host
   binds to `127.0.0.1` directly and the test still
   observes 5+5 from a single peer — the IP partitioning
   works.
5. **Retry-After is always emitted on 429.** Unit + contract
   tests assert this. The header is sourced from the lease
   metadata (`MetadataName.RetryAfter`), not hard-coded.
6. **ApiClient retries exactly ONCE.** Two 429s in a row
   throw `RateLimited`; a buggy server cannot make the UI
   loop. Tests cover the success-after-one-retry path, the
   second-429-throws path, the 65s ceiling clamp, and the
   missing-header skip.
7. **No DoS on broadcast at 1/sec.** The runbook calls out
   the per-second window as the broadcast policy's whole
   point; verify the partition factory chooses
   `TimeSpan.FromSeconds(1)` whenever `PermitsPerSecond > 0`.
8. **Test envOverrides knob is scoped.** The host-harness
   addition affects only callers who pass `envOverrides`;
   the existing globalSetup host still ships with no
   overrides, so the rest of the contract suite is
   unaffected.

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
S3 audit log, S4 log stream, S5 secrets, S6 NRT, S7
runbook completion.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere.
- `dotnet test --filter RateLimiting.RateLimiterPoliciesTests`
  6/6 green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`
  23/23 green (was 20; +3 rate-limit burst describes).
- `pnpm verify` green (covers the 182-test unit suite
  incl. the new 4 ApiClient cases, and the regenerated
  swagger/api.generated.ts).
- Manual: with the dev compose stack up, hit
  `/api/admin/auth/login` 10 times in <1s under the
  default 5/min limit; expect 5 first ones to reach the
  auth layer + 5×429 with `Retry-After: 60` (give or
  take, depending on where in the 60s window the burst
  fires).
