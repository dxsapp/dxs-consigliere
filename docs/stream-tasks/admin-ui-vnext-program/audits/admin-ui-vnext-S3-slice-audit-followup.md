---
created: 2026-05-18
type: audit-followup
parent: admin-ui-vnext-S3-slice-audit
status: applied
---

# Admin UI vNext — S3 slice-audit followup (MAJOR REVISION REQUIRED)

Codex S3 slice-audit on `274ee5e`: MAJOR REVISION REQUIRED
(0C / 2H / 6M / 3L). All 11 findings closed in this commit.

## H1 — Real SignalR pointed at non-existent hub route

**Verified:** backend maps `WalletHub.Route => "/ws/consigliere"`
(`src/Dxs.Consigliere/WebSockets/WalletHub.cs:26`), but the
factory defaulted real-mode SignalR to `"/wallethub"`.

**Revision applied:**

- New `src/lib/api/routes.ts` with `ADMIN_API_ROUTES`
  constants: `walletHubPath`, `authMe`, `authLogin`,
  `authLogout`. Single source of truth, ready for the S3
  followup codegen step.
- `factory.ts` re-exports the constants and the real-mode
  SignalRClient now defaults to `ADMIN_API_ROUTES.walletHubPath`.
- `AuthClient` uses `ADMIN_API_ROUTES.{authMe, authLogin,
  authLogout}` instead of inline strings.
- `SignalRClient.hubUrlForTests` accessor added so tests can
  pin the URL.
- New `factory.test.ts` (4 tests) asserts:
  - `resolveApiMode()` defaults to real.
  - `walletHubPath === "/ws/consigliere"` (verbatim backend
    constant).
  - Real-mode `signalR.hubUrlForTests` matches the constant.
  - Auth route constants exactly match the controller
    `[Route("api/admin/auth")]`.

## H2 — `signOut()` did not clear local user on network failure

**Verified:** `signOut()` delegated to `applyError()`, which
only cleared `user` on 401; network/server errors left the
stale authenticated user in place.

**Revision applied:**

- `signOut()` catch block now explicitly:
  - Sets `status = "anonymous"`.
  - Clears `user = null`.
  - Surfaces the error as `lastError` (visibility, not
    blocking).
- New test in `root.test.ts`:
  `signOut clears user even when the network call fails`.
  Drives a stub `IAuthClient` that rejects logout with
  `Network` error; asserts user is null + anonymous +
  lastError populated.

## M1 — Failed initial SignalR start left stale non-null connection

**Verified:** `start()`'s catch emitted offline but never
nulled `this.connection`; subsequent `start()` returned early
and `requireConnected()` only checked non-null, allowing
invokes on a dead SDK object.

**Revision applied:**

- New `connected: boolean` field; flipped to `true` only
  after `conn.start()` resolves.
- Start failure path now: `connected = false; connection =
  null; markOffline(); throw`.
- `onreconnecting` / `onreconnected` / `onclose` each
  toggle `connected` to match the underlying state.
- `requireConnected()` now checks `connection !== null && connected`
  with a sharper error message.

## M2 — Invalid login credentials silently treated as anonymous

**Verified:** the generic `applyError()` suppressed every
401 + cleared `lastError`. The login form rendered NO error
after `invalid_credentials` from the backend.

**Revision applied:**

- Split `applyError` into two methods:
  - `applySessionError(err)` — for `hydrate()`. 401 →
    anonymous + no lastError (canonical session-expired
    state).
  - `applyLoginError(err)` — for `signIn()`. ALWAYS surfaces
    `lastError`; status flips to `anonymous`.
- LoginPage already renders `auth.lastError` via the existing
  Typography element; the test now asserts the rendered
  error: `expect(screen.getByText(/invalid_credentials/))
  .toBeInTheDocument()`.

## M3 — Auth hydration not idempotent under StrictMode

**Verified:** repeat `useEffect` mounts (StrictMode) fired
two parallel `GET /me` requests; arrival-order race could
overwrite state.

**Revision applied:**

- `hydrate()` now returns the same in-flight promise via
  a module-level `WeakMap<AuthStore, Promise<void>>`.
- New test:
  `hydrate is idempotent — concurrent calls share one client
  request`. Spies on the IAuthClient.me() call count;
  asserts `inflightCalls === 1` for two parallel hydrate
  calls AND `=== 2` after the first resolves and a second
  hydrate runs (no permanent caching).

## M4 — Broadcast event `failReason` nullability drift

**Verified:** C# `BroadcastStateEvent` has `string FailReason
= null`; SignalR default JSON serializes null literals (not
omitted). The TS type was `failReason?: string` (absence
permitted, null not).

**Revision applied:**

- `EventMap.OnBroadcastStateChanged.failReason: string | null`
  (not optional).
- `MockSignalRClient` emits `failReason: null` on happy-path
  transitions.
- Existing bus + cleanup-harness test emit-shapes updated to
  include `failReason: null`.

## M5 — MockSignalR not idempotent on repeat `start()`

**Revision applied:**

- `MockSignalRClient.start()` early-returns when either
  timer is already set. Mirrors the real client's
  `if (this.connection) return`.
- New cleanup-harness test: `same-instance double start() is
  a no-op (S3-audit M5 idempotent)` — calls start() twice;
  asserts only one block tip per 20s, not two.

## M6 — Bundle headroom too thin for S8

**Revision applied:**

- Master.md S8 row updated to **REQUIRE** a route-level
  lazy boundary for DataGrid + X Charts (was nominally
  planned for S9/S10 only).
- S8 done-when now includes "per-route chunk reported
  separately from shell budget" so the breach surface
  is observable.

## L1 — Cleanup harness assertion weaker than its name

**Revision applied:** the "SignalR client stop() halts emit
cadence" test now keeps the subscription ACTIVE after
`stop()` (no `off()` call). If the interval leaked, the
counter would keep advancing. Plus a paired test
(`same-instance double start() is a no-op`) directly proves
M5's idempotency.

## L2 — `contracts/README.md` directionally useful but not executable

**Revision applied:**

- `contracts/README.md` rewritten with concrete commands:
  - Step 1 — exact `dotnet run --project ... --emit-swagger`
    invocation + backend slice owner (`public-api-and-realtime`).
  - Step 2 — exact `pnpm contracts:generate` /
    `pnpm contracts:check` scripts + `openapi-typescript`
    devDep version.
  - Step 3 — exact `pnpm test:contract` CI invocation +
    test fixture shape.
- New `src/admin-ui/tests/contract/auth.test.ts` with three
  `describe.skip(...)` cases documenting the eventual
  parity assertions. Replaces the `.gitkeep` placeholder.

## L3 — Unused `eslint-disable` comments

**Revision applied:** removed the two redundant
`// eslint-disable-next-line no-console` comments
(`bus.ts:97` + `pref.store.ts:105`). The ESLint policy
`no-console: ["warn", { allow: ["warn", "error"] }]` already
permits `console.warn` calls; the disables were no-ops.
`pnpm verify` is now clean.

---

## Summary

| Layer | Change |
|---|---|
| `lib/api/routes.ts` | NEW — single source of backend route strings (H1) |
| `lib/api/factory.ts` | imports/re-exports the constants (H1) |
| `lib/api/factory.test.ts` | NEW — 4 tests pin route parity (H1) |
| `lib/auth/client.ts` | uses route constants instead of inline strings (H1) |
| `lib/signalr/client.ts` | + `connected` flag + sharper requireConnected + start-failure cleanup + hubUrlForTests seam (H1 + M1) |
| `lib/events/bus.ts` | `failReason: string \| null` (M4) + unused-disable comment removed (L3) |
| `lib/mock/signalr.ts` | start() idempotent (M5) + emits `failReason: null` (M4) |
| `stores/root.ts` | hydrate idempotent via WeakMap (M3); applyError split into applySessionError + applyLoginError (M2); signOut catch clears user (H2) |
| `stores/pref.store.ts` | unused-disable comment removed (L3) |
| `stores/root.test.ts` | +3 new tests (M2 + M3 + H2 + 401-hydrate-no-error) |
| `screens/login/LoginPage.test.tsx` | invalid-credentials test now asserts rendered error message (M2) |
| `integration/cleanup-harness.test.tsx` | sharpened stop() test (L1); new same-instance-double-start test (M5); broadcast emits include `failReason: null` (M4) |
| `lib/events/bus.test.ts` | broadcast emit shape updated for failReason: null (M4) |
| `contracts/README.md` | concrete commands + script names (L2) |
| `tests/contract/auth.test.ts` | NEW — skip-fixtured scaffold for the parity test (L2) |
| `master.md` S8 row | route-level lazy boundary REQUIRED + per-route chunk budget separately reported (M6) |

Tests: 67 → 75 green (12 files).
Bundle: shell 198.39 KB gzip (under A1 M3's 200 KB);
SignalR chunk 14.44 KB gzip lazy.

S3 slice-gate cleared; S4 (Dashboard) opens.
