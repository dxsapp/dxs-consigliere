# wave-A2 S2 — slice-audit prompt

Audit target: wave-A2 S2 (ASP.NET-host contract-parity tests
+ admin-ui-contracts CI job) at HEAD `fbdff38` on
`codex/consigliere-vnext`. Diff range: `45aa706..fbdff38`.

---

You are auditing the **third + final slice of wave-A2**. It
closes half-2 of the wave-A1 closeout residual "swagger
codegen + ASP.NET-host parity test". Where S1 caught
compile-time drift (TS file changes when the C# schema
shifts), S2 catches RUNTIME drift — every admin REST endpoint
is hit against a real backend + a real RavenDB, and every
response body is ajv-validated against the swagger.json
schema.

Read first:

- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/master.md`
  (S2 row marked done at `fbdff38`; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/slices.md`
  § S2 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/audits/S1-slice-audit-followup.md`
  (the prior fold — S1's compile-time drift gate is
  complementary to this slice's runtime parity gate)
- Backend reference (consume-only):
  - `src/Dxs.Consigliere/appsettings.Test.json` (new)
  - `src/Dxs.Consigliere/Program.cs` (config-source ordering
    fix; env vars now win over the env-specific JSON re-load)

Cross-validate against the deliverable on commit `fbdff38`:

- `src/Dxs.Consigliere/appsettings.Test.json` — disabled
  backgrounds (best-effort — see scope notes below),
  RavenDb pointing at localhost, dedicated DbName
- `src/Dxs.Consigliere/Program.cs:65-75` — the
  `.ConfigureAppConfiguration` block now re-applies
  `AddEnvironmentVariables()` + `AddCommandLine(args)` so
  `RavenDb__Urls__0` actually wins
- `src/admin-ui/tests/contract/_host-harness.ts` —
  builds + spawns the assembly directly (NOT `dotnet run`,
  which wraps in a SIGTERM-eating shell), waits for
  "Now listening on:" in stdout, exposes
  `{ baseUrl, cookieJar, stop() }`
- `src/admin-ui/tests/contract/_schema-validator.ts` —
  ajv2020 wrapper; rewrites OpenAPI `#/components/schemas/X`
  refs to bare names; compiles a per-schema validator.
  `expectShape(name, body)` is the assertion API.
- `src/admin-ui/tests/contract/_session.ts` — module-scoped
  lazy host memo + setup wizard + admin sign-in. `beforeExit`
  hook cleans up the dotnet child.
- `src/admin-ui/tests/contract/auth.test.ts` — un-skipped, 2
  describes
- `src/admin-ui/tests/contract/admin.test.ts` — new, 10
  describes
- `src/admin-ui/vitest.contract.config.ts` — `singleFork:
  true` pool, `@` alias, 90s test + 180s hook timeouts
- `.github/workflows/ci-tests.yml` — new `admin-ui-contracts`
  job with RavenDB 7.1 service container,
  `RAVEN_PublicServerUrl=http://localhost:8080` so the
  topology hand-back is reachable; uploads
  `swagger.json` + `api.generated.ts` on failure
- `package.json` — `ajv@^8.20.0` + `ajv-formats@^3.0.1` as
  devDeps

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

### Backend test-mode

1. **`appsettings.Test.json` shape.** Disabled backgrounds
   (`BackgroundTasks.EnabledTasks: []`), but verify the
   binding actually empties the inherited set. In .NET
   IConfiguration, array binding MERGES by index — an empty
   array does NOT clear an existing populated array. Check
   the JungleBus background-task warnings in the host log:
   they fire because `EnabledTasks` from
   `appsettings.json` survives the merge. Flag as a known
   limitation (does not break tests — backgrounds run + warn
   but don't crash the host), and propose a fix path:
   either `DisabledTasks: [<every name>]` OR introduce a
   `DisableAll: bool` on `BackgroundTasksConfig`.
2. **Config-source ordering fix.** The custom
   `ConfigureAppConfiguration` now re-applies
   `AddEnvironmentVariables()` + `AddCommandLine(args)` AFTER
   the env-specific JSON re-load. Verify this matches the
   original intent (env vars + cmdline are the
   highest-priority sources). Spot-check that the
   DockerComposeE2E flow still works — its `ravendb:8080`
   URL comes from `appsettings.DockerComposeE2E.json` with
   no env-var override, so the fix should be a no-op there.
3. **No new backend public surface.** No new controllers,
   endpoints, or DTOs in this slice. Only:
   - `Program.cs` config-source ordering tweak
   - `appsettings.Test.json` (new)
   The backend's wire contract is unchanged.

### Harness correctness

4. **Build + spawn assembly directly.** Harness runs
   `dotnet build` once then spawns
   `dotnet <bin>/Dxs.Consigliere.dll` directly — NOT
   `dotnet run`. The `dotnet run` wrapper doesn't propagate
   SIGTERM to its child, which manifested as vitest
   hanging on globalSetup teardown ("Tests closed
   successfully but something prevents Vite server from
   exiting"). Verify the change matches.
5. **Kestrel listening regex.** The harness watches for
   `Now listening on:\s+(https?:\/\/[^\s]+)` on stdout
   AND stderr. The `Logging__LogLevel__Microsoft__Hosting__Lifetime
   = Information` env var keeps that line at INFO so it
   prints. Confirm the regex matches the actual Kestrel
   output (it does — verified locally with a Pixel-perfect
   regex match).
6. **Cookie jar.** The harness's `callApi` helper attaches
   `Cookie:` from the jar on every request + parses
   `Set-Cookie` responses. Verify that the session cookie
   from `/api/admin/auth/login` survives across describes
   in different spec files (it does — the module-scoped
   memo in `_session.ts` keeps the same HostHandle alive).
7. **Stop semantics.** `stop()` sends SIGTERM, then waits
   up to 10s, then SIGKILL. Verify the wait + escalation
   handles the dotnet child cleanly — the wave-A1 e2e flow
   has a similar harness pattern; cross-check the timing.
8. **`beforeExit` hook for teardown.** vitest doesn't
   guarantee per-spec-file teardown in singleFork mode;
   the `process.on("beforeExit")` in `_session.ts` is the
   only reliable cleanup. Verify the hook awaits the stop
   promise so the dotnet child actually exits.

### Schema-validation correctness

9. **OpenAPI `$ref` rewrite.** Ajv2020 rejects `$id`
   containing `#`. The validator walks every schema
   recursively + rewrites `$ref: "#/components/schemas/X"`
   to `$ref: "X"` so Ajv's reference resolution finds
   each registered schema by name. Spot-check by
   intentionally introducing a $ref cycle in
   swagger.json — does ajv blow up gracefully or
   infinite-loop?
10. **Per-endpoint shape gate.** Each `describe` block hits
    the endpoint, parses JSON, and calls
    `expectShape(name, body)`. The current schema is
    permissive (Swashbuckle defaults every property to
    `nullable: true` + no `required` array), so the
    assertion catches FIELD-LEVEL drift (renames, type
    changes, removed properties) but NOT a missing-but-
    expected-required field. Flag as wave-A3 NRT residual.
11. **Wave-A1 H1 + H2 regression coverage.** Manually
    inject the wave-A1 audit H1 drift (rename
    `historyReadiness` to `historyStatus` in a C# DTO)
    and verify the parity gate fires red on the
    Address/Token endpoints. If the gate misses, that's a
    bug in the schema generation OR the test surface.

### Test surface coverage

12. **12 describes cover the admin UI's read surface.**
    Auth (me + login), p2p (health, peers, headers tip +
    recent, alerts), metrics (sources), providers,
    tracked (addresses + tokens), setup (status).
    Anything the UI reads that this suite doesn't cover?
    Spot-check `src/admin-ui/src/lib/admin/admin-client.ts`
    — every `IAdminClient` method should have a parity
    describe.
13. **No write-side coverage.** Force-rebroadcast (POST
    /api/tx/broadcast) is NOT covered. The wave-A1 contract
    is "broadcast posts a receipt"; that's exercised by
    the e2e mock flow. Flag whether real-backend write
    coverage belongs here OR in a separate wave (A4-ish).
14. **Setup wizard is a side-effect of the suite.** The
    first `ensureAdminSession()` call walks the wizard;
    `setup/status` returns `setupCompleted: true` for the
    rest of the run. Subsequent re-runs against the same
    RavenDB DbName would fail with 409. Verify CI
    re-creates the RavenDB service container per run
    (default behaviour — service containers are
    per-job-fresh).

### CI workflow

15. **`admin-ui-contracts` job.** Depends on `admin-ui`;
    matches the topology (Node 22 + setup-dotnet 9.0.x +
    pnpm). RavenDB 7.1 service container; `RAVEN_PublicServerUrl
    = http://localhost:8080` so the topology negotiation
    hands back a reachable URL (the local-dev fix that
    bit S2 during development).
16. **Health check.** `--health-cmd "wget -q --spider
    http://localhost:8080/setup/alive"` runs every 5s with
    a 30-attempt cap (~150s). Verify the cap is enough for
    the cold-start RavenDB on a small CI runner.
17. **Job timeout.** 25 min, vs 20 for admin-ui + 20 for
    e2e. Reasonable for cold dotnet build + Raven boot +
    setup wizard + 12 describes. Flag if a worst-case run
    could push past 25.
18. **Artifact on failure.** Uploads `swagger.json` +
    `api.generated.ts` so an audit can diff the deltas
    that caused the drift. Flag if any other input (e.g.
    appsettings.Test.json, harness logs) should also be
    in the artifact.

### Determinism + scope

19. **Slice scope.** No new TS DTOs in screens, no new
    backend endpoints, no UI changes. Pure verification
    layer. The wave-A1 contract is preserved.
20. **DTO drift detected by the GATE.** If a future
    backend wave renames `P2pHealthDto.PoolSize` →
    `PoolCount`, what fails first — S1's `pnpm
    contracts:check` (which would catch the swagger
    change) or S2's `expectShape` (which would catch the
    actual response body)? Both should fail; flag if one
    silently passes.
21. **Idempotency across re-runs.** Running
    `pnpm test:contract` twice in a row against the same
    Raven container: second run hits the
    `setup_already_completed` 409 in `completeSetupIfNeeded`
    — handled by the `if (json.setupCompleted === true)
    return;` short-circuit. Verify the second-run path
    re-signs in successfully with the same credentials.

### Cross-cutting + invariants

22. **No screen-code changes.** `git diff
    45aa706..fbdff38 -- src/admin-ui/src/screens` returns
    empty. The parity gate is purely under
    `src/admin-ui/tests/contract/` + config + CI.
23. **No legacy admin references.** New files don't reach
    into `wwwroot/legacy-admin/**`.
24. **`pnpm verify` chain intact.** `contracts:check`
    still wins/fails as before; the contract-parity
    `pnpm test:contract` is OUTSIDE `pnpm verify` (too
    heavy for the inner loop). Confirm contract job runs
    in CI separately + can be invoked locally via
    `pnpm test:contract`.

## Verdict format

End with:

```
Verdict: APPROVE | APPROVE WITH CHANGES | MAJOR REVISION REQUIRED
Critical findings: <count>
High findings: <count>
Medium findings: <count>
Low findings: <count>
Headline: <one sentence>
```

Then per-finding detail (`C1`, `H1`, `M1`, `L1` etc.):

- Severity
- Slice (S2 / wave-A2-level / program-level)
- Issue (1-2 sentences with `file:line` where applicable)
- Recommended fix (concrete)
