# wave-A2 S0 — slice-audit prompt

Audit target: wave-A2 S0 (public `/setup` wizard) at HEAD
`ce36377` on `codex/consigliere-vnext`. Diff range:
`ff0f54f..ce36377`.

---

You are auditing the **first slice of wave-A2**. It closes
the wave-A1 closeout residual "first-run install requires a
hand-typed `curl POST /api/setup/complete`" — a brand-new
operator now walks a four-step UI wizard from `/setup` and
lands signed-in.

The slice does not touch any backend C# code. It mirrors
existing `SetupOptionsResponse` + `SetupCompleteRequest`
shapes on the TS side, builds a new MobX store + page +
four step components, wires a public `/setup` route outside
`AuthGuard`, and updates `AuthGuard` so unauthenticated
visitors on a fresh install land on `/setup` rather than
`/login` (which they have no credentials for).

Read first:

- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/master.md`
  (the approved plan; S0 row marked done at `ce36377`)
- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/slices.md`
  § S0 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S7-S12-slice-audit-followup.md`
  (the prior fold — esp. the per-screen MobX store
  discipline + the StrictMode `disposed` flag pattern)
- `/Users/imighty/Code/docs/frontend-principles.md` §6
  (cleanup), §8 (error normalization), §10–11 (MobX store
  discipline)
- Backend reference (consume-only — diff must be empty):
  - `src/Dxs.Consigliere/Controllers/SetupController.cs`
    (the three endpoints the wizard consumes:
    `GET /api/setup/{status,options}`,
    `POST /api/setup/complete`)
  - `src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs`
    (+ nested `AdminProviderConfigUpdateRequest.cs`)
  - `src/Dxs.Consigliere/Dto/Responses/Setup/SetupStatusResponse.cs`
    (+ all nested options DTOs)
  - `src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs:111`
    (the backend rule that demands `BlockSubscriptionId`)

Cross-validate against the deliverable on commit `ce36377`:

- `src/admin-ui/src/types/admin.ts` — new SetupOptions* +
  SetupComplete* DTO interfaces
- `src/admin-ui/src/lib/api/routes.ts` —
  `SETUP_OPTIONS_PATH` + `SETUP_COMPLETE_PATH`
- `src/admin-ui/src/lib/admin/admin-client.ts` — new
  `getSetupOptions` + `completeSetup` methods on
  `IAdminClient` + `AdminClient`
- `src/admin-ui/src/lib/mock/admin.ts` — localStorage-
  persisted setup state + `getSetupOptions` (dynamic-imports
  seed) + `completeSetup` (writes both setup state + wizard
  auth credentials)
- `src/admin-ui/src/lib/mock/admin-systems-seed.ts` —
  `seedSetupOptions(status)`
- `src/admin-ui/src/lib/mock/auth.ts` — `statusResponse()`
  now reads mock setup state; `login()` first checks the
  wizard-chosen credentials, falls back to
  `operator/consigliere`
- `src/admin-ui/src/app/App.tsx` — new lazy
  `SetupWizardPage` route at `/setup`, outside `AuthGuard`,
  inside `ThemeProvider`
- `src/admin-ui/src/app/AuthGuard.tsx` — pre-`/login`
  branch that redirects to `/setup` when
  `auth.setupRequired === true`
- `src/admin-ui/src/screens/login/LoginPage.tsx` — the
  "Setup required" Alert gains a "Go to setup" action
- `src/admin-ui/src/screens/setup-wizard/setup-wizard.store.ts`
  + `.test.ts`
- `src/admin-ui/src/screens/setup-wizard/SetupWizardPage.tsx`
  + `.test.tsx`
- `src/admin-ui/src/screens/setup-wizard/steps/{Step1AdminAccess,
  Step2Providers, Step3BlockSync, Step4Review}.tsx`
- `src/admin-ui/tests/e2e/setup-wizard.spec.ts` (new)
- `src/admin-ui/tests/e2e/_setup-state.ts` (new helper) +
  every existing e2e spec calls `seedSetupCompleted(page)`
  so the AuthGuard redirect doesn't bounce them to `/setup`
- `src/admin-ui/.gitignore` — `.env` family excluded
- `src/admin-ui/src/screens/dashboard/dashboard.store.test.ts`
  + `src/admin-ui/src/screens/p2p/p2p.store.test.ts` — stub
  surfaces extended for the two new client methods
- `src/admin-ui/src/screens/setup/setup.store.test.ts` —
  seeds localStorage so the prior assertion still passes
  against the now-conditional mock

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

### DTO + contract parity

1. **SetupOptions DTO parity.** Every field in
   `SetupOptionsResponse.cs` (and its eight nested types) is
   mirrored in `types/admin.ts` with correct name + type +
   nullability. Spot-check the `*Defaults*` and `*Allowed*`
   variants individually.
2. **SetupCompleteRequest parity.** The wizard's
   `buildRequest()` produces a payload that the backend's
   `SetupWizardService` will accept verbatim. The two
   `blockSubscriptionId` fields (one in `providers.junglebus`,
   one in top-level `blockSync`) carry the same value —
   matches the backend rule that ignores the
   `providers.junglebus.blockSubscriptionId` when empty and
   copies from `blockSync.blockSubscriptionId` at line 116
   of `SetupWizardService.cs`.
3. **Frozen-shape respect.** The TS request matches the C#
   request property names exactly (`enabled`, `username`,
   `password`, `rawTxPrimaryProvider`, etc.). Any field
   rename would silently drop on the wire because ASP.NET
   uses default camelCase binding.

### First-run UX flow

4. **AuthGuard → /setup branch.** A fresh visit to any
   guarded route while
   `auth.setupRequired && !auth.isAuthenticated` lands on
   `/setup` (not `/login`). Verify the conditional order:
   the `setupRequired` branch must come before the
   `LOGIN_PATH` branch.
5. **/setup is outside AuthGuard.** The route is declared
   inside `BrowserRouter` but outside the AuthGuard
   wrapper. An anonymous visitor with no cookie reaches it
   without bouncing.
6. **Already-completed install** behavior at `/setup`:
   `SetupWizardPage` mounts → `getSetupOptions()` resolves
   → `options.status.setupCompleted === true` → returns
   `<Navigate to={LOGIN_PATH} replace />`. Verify no
   wizard chrome flashes briefly before the redirect.
7. **Post-submit auth re-hydrate.** After `completeSetup`
   resolves, the page calls `auth.hydrate()` THEN
   navigates to `/login`. The LoginPage banner must reflect
   the new state (no "Setup required" Alert, no Go-to-setup
   action). Race condition check: what if `auth.hydrate()`
   rejects? The current code awaits + then navigates
   unconditionally — flag if the navigation could leave
   `auth.setupRequired === true` in a recoverable error
   path.
8. **"Go to setup" action on LoginPage.** When
   `auth.setupRequired === true`, the Alert renders the
   action button (test-id `login-go-to-setup`). The button
   navigates to `/setup` via `useNavigate()`, not a hard
   anchor — preserves SPA state.

### MobX store correctness

9. **`SetupWizardStore` follows wave-A1 detail-store
    pattern.** No permanent `disposed` flag; idempotency
    via the inflight controller alone; `start()` is safe to
    call under StrictMode double-mount. Trace through:
    mount → start() → unmount cleanup → dispose() →
    re-mount → start() again — store loads fresh options
    cleanly.
10. **`makeAutoObservable(this, {}, { autoBind: true })`.**
    Standard recipe. Spot-check that the per-step form
    objects (`admin`, `providers`, `blockSync`) are
    reassigned (`this.admin = {...}`) rather than mutated
    in place — mutation would break MobX observability.
11. **Step navigation guards.** `goNext()` refuses to
    advance when the current step has errors. `goBack()`
    refuses below 1. `jumpTo(target)` allows backwards
    freely; allows forwards only when every skipped step
    has zero errors. Test the edge case `jumpTo(4)` from
    step 1 with everything except the password
    confirmation set — must stay on step 1.
12. **`canSubmit` semantics.** True only when
    `status === "ready"`, `step === 4`, and all 3 prior
    steps validate clean. Step 4's own errors aggregate
    every step's errors — so a step-1 error from a back-
    navigation surfaces in the Review step.

### Validation rules

13. **Admin step validation.** Username ≥ 3 chars, password
    ≥ 8 chars, confirm matches. The backend has its own
    rules (BCrypt min-cost, etc.) — the TS rules are a
    superset, not contradictory.
14. **Provider step validation.** Each selected primary is
    in the allowed list; bitailsTransport is allowed; URLs
    parse via `new URL()` AND use http/https. ZMQ URLs
    are NOT required (they're optional even in the C#
    DTO).
15. **BlockSync step validation.** `blockSubscriptionId`
    required (mirrors backend rule at
    `SetupWizardService.cs:111`). BaseURL parses as http/
    https. Spot-check: empty subscription ID surfaces a
    visible error before submit, not as a 400 from the
    backend.
16. **Submit error surfaces.** Backend 409
    `setup_already_completed` (the mock fires this if the
    wizard is re-submitted) renders as `store.error` on
    the page; `status` reverts to `ready`. Flag if any
    error path leaves `status === "submitting"` (button
    would stay disabled forever).

### Mock layer + e2e plumbing

17. **Mock setup state persistence.** Both keys
    (`consigliere-admin/mock-setup-state/v1` and
    `consigliere-admin/mock-auth-credentials/v1`) are
    cleared by `src/test-setup.ts`'s `afterEach` so vitest
    cases stay hermetic. Verify the test-setup teardown
    covers them.
18. **MockAuthClient credential precedence.** Wizard-
    chosen credentials are checked first; the
    `operator/consigliere` seed is the fallback. Existing
    e2e specs that use the seed pair still pass. New
    setup-wizard spec uses `admin-a2 / ConsigliereA2!` and
    those credentials work post-redirect.
19. **`seedSetupCompleted(page)` injection.** Every
    existing e2e spec calls it at the top of the test
    body. Spot-check that the `addInitScript` runs BEFORE
    the first `page.goto`, so AuthGuard sees the seeded
    state on first mount.
20. **AuthGuard hydration ordering.** The redirect to
    `/setup` only fires AFTER `auth.hydrate()` resolves.
    The App.tsx bootstrap chain (`Promise.all([hydratePrefStore,
    auth.hydrate()])`) is intact — verify the early-return
    `if (!root) return null;` still blocks render until
    both finish.

### Cleanup discipline (Core Rule §6)

21. **`SetupWizardStore.dispose()`** aborts the inflight
    controller (covered by the
    `setup-wizard.store.test.ts` "dispose aborts in-flight
    fetch" case). Idempotent calls (`dispose()` twice in a
    row) are no-ops.
22. **`SetupWizardPage` unmount.** The page's `useEffect`
    returns `store.dispose()`. If the operator hits the
    browser back button mid-wizard, the page unmounts and
    the in-flight POST is aborted.
23. **Post-submit effect cancellation.** The post-submit
    `useEffect` returns a cleanup that sets `cancelled =
    true`. If the page unmounts during the
    `await auth.hydrate()` window, the navigate call is
    skipped.

### Bundle + test surface

24. **SetupWizardPage chunk is lazy.** `App.tsx` uses
    `lazy(() => import(...))` for the wizard. Per
    `pnpm inventory`, the chunk is 4.41 KB gzip (well
    under the 250 KB per-route ceiling).
25. **Shell budget.** 194.58 KB gzip — under the 200 KB A1
    M3 ceiling. The new TS types in `types/admin.ts` are
    erased at runtime; the mock state-reader helper is a
    handful of bytes. No shell-budget regression expected.
26. **Test coverage.** vitest: 9 store cases + 4 page
    cases. e2e: 1 spec (chromium + mobile-chromium). Flag
    any branch not covered — particularly the
    setup-already-completed redirect, the
    `auth.hydrate()` failure path, the "wizard mid-edit
    then operator visits /login directly" race.

### Cross-cutting + invariants

27. **No backend touched.**
    `git diff ff0f54f..ce36377 -- src/Dxs.Consigliere src/Dxs.Common`
    must be empty.
28. **No legacy admin app references.** `git grep` over
    the new files for `legacy-admin` returns zero hits.
29. **Wave-A1 store-pattern conformance.** Compare the
    new store against `address-detail.store.ts` /
    `setup.store.ts` (other read+commit detail stores).
    Same idempotency strategy, same error shape, same
    teardown.
30. **Master.md ledger freshness.** S0 row marked done
    with the commit hash; Delivery Notes row reflects the
    actual `ce36377`.

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
- Slice (S0 / wave-A2-level)
- Issue (1-2 sentences with `file:line` where applicable)
- Recommended fix (concrete)
