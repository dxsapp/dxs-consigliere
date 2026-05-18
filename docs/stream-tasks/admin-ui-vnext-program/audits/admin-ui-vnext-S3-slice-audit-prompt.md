# Admin UI vNext — S3 slice-audit prompt

Audit target: S3 (API + SignalR + mock + event bus + auth +
cleanup harness) at commit `274ee5e` on
`codex/consigliere-vnext`. Run sync; this gates S4+ open
and closes the foundational quartet.

---

You are auditing the **fourth and final foundational slice**.
S3 lands the network + event-bus + cookie auth + cleanup
harness that every screen slice (S4-S10) depends on.

Read:

- `docs/stream-tasks/admin-ui-vnext-program/master.md` (the
  approved plan; S3 row marked done with the contract-parity
  test recorded as a residual)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S2-slice-audit-followup.md`
  (S2 baseline)
- `/Users/imighty/Code/docs/frontend-principles.md` §6
  (cleanup), §8 (error normalization), §16-17 (route-driven
  hydration)
- Backend reference:
  - `src/Dxs.Consigliere/Controllers/AdminAuthController.cs`
  - `src/Dxs.Consigliere/Dto/{Requests,Responses}/AdminAuth*.cs`
  - `src/Dxs.Consigliere/WebSockets/IWalletHub.cs`
  - `src/Dxs.Consigliere/WebSockets/{BlockTipDto,ReorgEventDto}.cs`

Cross-validate against the S3 deliverable at `274ee5e`:

- `src/admin-ui/src/types/auth.ts`
- `src/admin-ui/src/lib/auth/client.ts`
- `src/admin-ui/src/lib/events/bus.ts` + `bus.test.ts`
- `src/admin-ui/src/lib/signalr/client.ts`
- `src/admin-ui/src/lib/mock/auth.ts` + `signalr.ts` +
  `signalr.test.ts`
- `src/admin-ui/src/lib/api/factory.ts`
- `src/admin-ui/src/stores/root.ts` + `root.test.ts`
- `src/admin-ui/src/app/App.tsx`
- `src/admin-ui/src/screens/login/LoginPage.tsx` +
  `LoginPage.test.tsx`
- `src/admin-ui/src/components/shell/AppHeader.tsx`
- `src/admin-ui/integration/cleanup-harness.test.tsx`
- `src/admin-ui/contracts/README.md`
- `src/admin-ui/package.json` (new `@microsoft/signalr` dep)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **DTO parity (A1 H4).** Verify `src/types/auth.ts` matches
   `AdminAuthStatusResponse` + `AdminLoginRequest` verbatim
   (field names, types, nullability). The audit explicitly
   accepts the S3 deferral of the swagger.json codegen step
   — but the hand-written DTOs MUST be drift-free against
   `src/Dxs.Consigliere/Dto/{Requests,Responses}/AdminAuth*.cs`.

2. **EventBus events parity.** Verify `bus.ts` `EventMap`
   has accurate shapes for `OnNewBlock`, `OnReorg`,
   `OnBroadcastStateChanged` against the C# DTOs
   (`BlockTipDto`, `ReorgEventDto`,
   `BroadcastStateEvent`). Flag any field drift.

3. **Auth flow correctness (A1 H2).** Step through:
   - On app mount: `auth.hydrate()` calls
     `GET /api/admin/auth/me` and routes 401 → anonymous
     (NOT error). The API client also redirects 401 to
     `/login` — verify those two redirects compose without
     loop.
   - `signIn(creds)` POSTs `/login`; success applies
     `authenticated` + user; failure (401/400/409) flows
     into `applyError`. The store NEVER ends up in
     `idle` after the first interaction.
   - `signOut()` POSTs `/logout` AND clears `user` even on
     network failure (verify the catch branch).

4. **Mock parity (Core Rule §12).** `MockAuthClient`
   returns the SAME `AdminAuthStatusResponse` shape as the
   real backend (all 6 fields), and the seed transitions
   are realistic (login → authenticated; logout → anonymous;
   bad creds → 401 AppError). `MockSignalRClient` emits the
   same `EventMap` payload shapes the real hub would.

5. **Event-bus semantics.** `bus.on` returns a working
   unsubscribe (set deletion + key cleanup when empty);
   `emit` is sync; one throwing handler does NOT block the
   rest. The cleanup-harness tests cover these — verify the
   assertions are sharp (e.g. `listenerCount === 0` after
   unsub, not just "no event fired").

6. **SignalR cleanup discipline (Core Rule §6 / A1 M2).**
   - `start()` is idempotent (early-return on `connection !=
     null`)?
   - `stop()` clears the stale timer + nulls the connection
     + emits `ConnectionStateChanged:"offline"`?
   - `onreconnecting` → stale-after-`staleAfterMs` timer
     fires? Verify the clear-then-set logic doesn't leak.
   - Tests in `cleanup-harness.test.tsx` adequately pin "no
     emits after stop" AND "double-mount cleanup keeps
     one client running".

7. **Dynamic import correctness.** `signalr/client.ts`
   dynamic-imports `@microsoft/signalr` inside `start()`.
   - `import type { HubConnection }` at top is types-only
     (erased at runtime — confirm via build output).
   - The import resolves to the correct chunk (build output
     shows `index-*.js` 55.80 KB containing the SignalR
     SDK — verify it's not double-bundled).
   - `requireConnected` no longer uses `HubConnectionState`
     since that's in the dynamic module — flag if that
     weakens the "connected" check meaningfully (was
     `state === Connected`; now is "field non-null after a
     successful start()").

8. **API factory env switch.** `resolveApiMode()` reads
   `import.meta.env.VITE_API_MODE`. Verify:
   - Default (`undefined`) = real.
   - Only literal `"mock"` flips to mock.
   - The factory is called ONCE per RootStore construction
     (i.e. tests can inject a pre-built `ApiFactoryResult`).

9. **App.tsx bootstrap correctness.** The new `useEffect`:
   - Runs `hydratePrefStore` + `auth.hydrate()` in parallel
     via `Promise.all`. Verify failures don't block render
     (auth.hydrate catches internally; prefs is
     failure-safe by S1).
   - `signalR.start()` is best-effort (caught + ignored).
   - `cancelled` guard prevents setting `hydrated` after
     unmount — but does NOT cancel the in-flight Promise.all
     chain. Flag if this could cause a race (the chain
     completes asynchronously after unmount, but the only
     observable effect is store mutation, which is
     orphaned).
   - `signalR.start()` is NEVER awaited inside `Promise.all`
     — so a delay there doesn't block render. Good. But
     verify it ALSO runs only when both auth + prefs
     succeed (currently it runs UNCONDITIONALLY after
     Promise.all resolves, regardless of auth status —
     should it?).

10. **No backend touched.** Diff
    `src/Dxs.Consigliere/**` for the S3 commit — must be
    empty. New backend coupling is via consume-only DTO
    mirroring.

11. **Cleanup harness sharpness.** Each of the 5
    integration cases:
    - "unsubscribed handlers don't receive emits" — proves
      the unsub binding correctly.
    - "repeat sub/unsub leaves zero listeners" — pins a
      50-iteration leak loop.
    - "stop() halts emit cadence" — fake-timer advance
      proves no further `OnNewBlock`.
    - "subscribe AFTER stop yields no events" — pins the
      stopped-client contract.
    - "double-mount cleanup keeps one client running" — the
      StrictMode-safety pin.
    Flag any case that's structurally weak (e.g. a fixed
    pass count from coincidence rather than logic).

12. **Test coverage gap.** What is NOT tested:
    - Real `SignalRClient.start()` — can't be without a
      hub, but is the dynamic-import path covered by a
      type-only test or smoke?
    - Real `AuthClient.login()` — the integration only
      covers `MockAuthClient`. The S3 followup will add
      the contract-parity test against ASP.NET; flag if
      this is too thin for the foundational slice.

13. **AppHeader logout async-await.** `handleLogout` now
    `await`s `signOut()` then navigates. Verify the IconButton
    onClick wraps it correctly (no unhandled-promise warning
    if the signOut throws? The AuthStore catches internally,
    but pin the contract).

14. **App.tsx + StrictMode interaction.** Under StrictMode
    the effect double-fires:
    - `hydratePrefStore` is idempotent (S1 fix).
    - `auth.hydrate()` is NOT idempotent — two parallel
      GETs would race. Flag as a finding (likely M); the
      fix is similar to the prefs WeakMap.

15. **Login redirect on success.** `onSubmit` calls
    `signIn` then navigates to `state.from ?? LANDING_PATH`
    when the returned boolean is true. Verify:
    - The form doesn't accidentally lose the password from
      state if the user re-tries.
    - `submitting` flag correctly disables the inputs +
      button.
    - 401 path leaves `submitting` false (re-enabled).

16. **`requireConnected` weak check.** Inside SignalRClient,
    `requireConnected()` only checks `this.connection`
    non-null, not `state === Connected`. If `start()`
    rejected mid-way, `connection` is non-null but the
    underlying SDK is in a non-Connected state. Flag.
    (Currently start() catches + sets `markOffline` but
    doesn't null the connection.)

17. **Mock SignalR cadence realism.** 20 s block + 8 s
    broadcast cadence. Flag if these need configurability
    for screen-level integration tests (today they're
    constants).

18. **Bundle budget headroom.** Shell 198.19 KB gzip after
    S3 — TWO KB below the A1 M3 ceiling. Without route-level
    lazy boundaries for X DataGrid (S8) and X Charts (S9),
    those slices will breach 200 KB. Flag as a planning
    risk; the program already commits to per-route lazy in
    S9/S10 done-when, but S8 should also be lazy.

19. **`contracts/README.md` resolution path.** The S3
    deferral of the codegen step is documented. Verify the
    followup task is concrete enough that a future operator
    can pick it up (links to specific commands, expected
    file paths).

20. **Doc freshness.** README §Status, master.md S3 row
    status, and any other doc that names a specific path.
    Spot-check the master.md row to confirm it accurately
    notes the contract-parity residual.

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
- Slice (S3 or program-level)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
