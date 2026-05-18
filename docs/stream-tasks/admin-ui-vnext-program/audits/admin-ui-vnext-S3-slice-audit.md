# Admin UI vNext - S3 slice audit

Target commit: `274ee5e` (`feat(admin-ui): wave-vnext S3 - API + SignalR + mock + event bus + cleanup harness`)
Reviewer: Codex
Date: 2026-05-18

Scope: S3 API + SignalR + mock + auth client + cleanup harness + event bus at `274ee5e`. The branch head had moved past the target, so verification was run from detached worktree `/tmp/dxs-s3-audit-274ee5e`.

Covered dimensions:

1. Admin auth DTO parity
2. SignalR event payload parity
3. Auth hydrate / sign-in / sign-out correctness
4. Mock auth and mock SignalR parity
5. EventBus unsubscribe and handler-isolation semantics
6. SignalR cleanup and stale-state discipline
7. Dynamic import and bundle split behavior
8. API factory environment switch
9. App bootstrap and StrictMode interaction
10. Backend no-touch rule
11. Cleanup harness sharpness
12. Real-client test coverage gaps
13. AppHeader logout flow
14. Login redirect and retry UX
15. Bundle budget headroom
16. Contract followup documentation
17. Doc freshness

Verification:

- `pnpm install --frozen-lockfile && pnpm verify` from `/tmp/dxs-s3-audit-274ee5e/src/admin-ui`: pass.
- Vitest: 11 files, 67 tests passed.
- Build output: SignalR SDK chunk `index-CwFrb3pF.js` is `55.80 kB` / `14.44 kB gzip`; shell chunk `index-wln3jbFT.js` is `629.57 kB` / `198.19 kB gzip`.
- Dynamic import check: SignalR SDK code appears in the separate `index-CwFrb3pF.js` chunk; the type-only `HubConnection` import did not pull the SDK eagerly.
- ESLint emitted two warnings: unused `eslint-disable` directives in `src/lib/events/bus.ts:93` and `src/stores/pref.store.ts:105`.
- `git diff --name-only 274ee5e^ 274ee5e -- src/Dxs.Consigliere`: empty. S3 did not touch backend code.

Positive coverage:

- `src/admin-ui/src/types/auth.ts:10` mirrors the nominal C# auth DTO field names from `AdminAuthStatusResponse` and `AdminLoginRequest`.
- `EventBus` has synchronous fan-out, working unsubscribe, key cleanup, and throwing-handler isolation (`src/admin-ui/src/lib/events/bus.ts:67`, `src/admin-ui/src/lib/events/bus.ts:84`).
- `resolveApiMode()` defaults to real mode and only literal `"mock"` enables mocks (`src/admin-ui/src/lib/api/factory.ts:21`).
- `RootStore` accepts an injected `ApiFactoryResult`, so tests do not have to re-run the factory (`src/admin-ui/src/stores/root.ts:40`).
- `AppHeader.handleLogout` awaits `auth.signOut()` before navigation, and `AuthStore.signOut()` catches internally, so the click handler has no unhandled rejection path (`src/admin-ui/src/components/shell/AppHeader.tsx:53`).

## Findings

### H1 - Real SignalR points at a non-existent hub route

Severity: HIGH
Slice: S3

Issue: The backend maps the wallet hub at `WalletHub.Route => "/ws/consigliere"` (`src/Dxs.Consigliere/WebSockets/WalletHub.cs:26`, `src/Dxs.Consigliere/Setup/SignalRSetup.cs:43`), but the S3 API factory defaults the real client to `"/wallethub"` (`src/admin-ui/src/lib/api/factory.ts:38`, `src/admin-ui/src/lib/api/factory.ts:57`). In real mode, `SignalRClient.start()` will negotiate against the wrong URL, so all S4-S10 real-time consumers inherit a dead connection.

Recommended fix: Change the default hub URL to `"/ws/consigliere"` and add a contract/unit test that asserts `createApiClients({ bus }).signalR` is configured with `WalletHub.Route` (or centralize the route constant in a generated/admin contract file).

### H2 - `signOut()` does not clear the local user on network failure

Severity: HIGH
Slice: S3

Issue: `AuthStore.signOut()` catches logout failures and delegates to `applyError()` (`src/admin-ui/src/stores/root.ts:125`), but `applyError()` only clears `user` for 401s and leaves the previous authenticated user in place for network/server errors (`src/admin-ui/src/stores/root.ts:168`). This violates the S3 prompt requirement that logout clears `user` even when the network call fails.

Recommended fix: In `signOut()`, clear `user`, set `status = "anonymous"` or a named signed-out/error state in a `finally`/catch path, and keep `lastError` only as optional feedback. Add a test with an `IAuthClient.logout()` rejection proving `user` is null afterward.

### M1 - Failed initial SignalR start leaves a stale non-null connection

Severity: MEDIUM
Slice: S3

Issue: `SignalRClient.start()` assigns `this.connection = conn` before `conn.start()` succeeds (`src/admin-ui/src/lib/signalr/client.ts:71`). If `conn.start()` rejects, the catch emits offline but never nulls `this.connection` (`src/admin-ui/src/lib/signalr/client.ts:75`). Later `start()` calls return early (`src/admin-ui/src/lib/signalr/client.ts:47`), and `requireConnected()` only checks non-null (`src/admin-ui/src/lib/signalr/client.ts:105`), so a failed first start can permanently block retry and allow invokes on a disconnected SDK object.

Recommended fix: On start failure, clear the stale timer, set `this.connection = null`, and have `requireConnected()` verify `connection.state === "Connected"` without eagerly importing the enum (for example compare the string state or keep a local connected flag set only after successful start).

### M2 - Invalid login credentials are silently treated as anonymous

Severity: MEDIUM
Slice: S3

Issue: `ApiClient` converts every 401 into a generic `Unauthorized` AppError (`src/admin-ui/src/lib/api/client.ts:54`), and `AuthStore.applyError()` suppresses every 401 by setting anonymous and clearing `lastError` (`src/admin-ui/src/stores/root.ts:172`). For `POST /api/admin/auth/login`, backend 401 means `{ code = "invalid_credentials" }` (`src/Dxs.Consigliere/Controllers/AdminAuthController.cs:44`), but the login page will show no error after a bad password; the S3 test named "invalid credentials show an error" does not assert any rendered error (`src/admin-ui/src/screens/login/LoginPage.test.tsx:66`).

Recommended fix: Treat login 401 differently from hydrate/session-expired 401. Preserve `invalid_credentials` as `lastError` for `signIn()` while keeping `hydrate()` 401 as anonymous. Add a test that bad credentials render an error and leave the form enabled.

### M3 - Auth hydration is not idempotent under StrictMode

Severity: MEDIUM
Slice: S3

Issue: `App.tsx` calls `root.auth.hydrate()` inside the mount effect (`src/admin-ui/src/app/App.tsx:31`), and `AuthStore.hydrate()` always starts a new `client.me()` call (`src/admin-ui/src/stores/root.ts:102`). React StrictMode can double-run the effect; unlike `hydratePrefStore`, auth has no in-flight promise/cache guard, so two `/api/admin/auth/me` requests can race and overwrite state in arrival order.

Recommended fix: Add an in-flight hydrate promise to `AuthStore.hydrate()` (or a root-level helper mirroring the pref-store pattern) and test that two immediate hydrate calls share one client request.

### M4 - Broadcast event `failReason` nullability drifts from the backend contract

Severity: MEDIUM
Slice: S3

Issue: The backend `BroadcastStateEvent` has `string FailReason = null` (`src/Dxs.Consigliere/WebSockets/IWalletHub.cs:33`), and SignalR JSON protocol does not configure null omission. The S3 `EventMap` models this as `failReason?: string` (`src/admin-ui/src/lib/events/bus.ts:42`), which permits absence but not the real `null` value.

Recommended fix: Change the event type to `failReason: string | null` if the backend always serializes the property, or configure/prove null omission and document that convention. Add a parity test fixture for `OnBroadcastStateChanged` with a failed and non-failed transition.

### M5 - Mock SignalR is not idempotent on repeated `start()` calls

Severity: MEDIUM
Slice: S3

Issue: The real client returns early when `connection` is non-null (`src/admin-ui/src/lib/signalr/client.ts:47`), but `MockSignalRClient.start()` always creates new intervals and overwrites the timer handles (`src/admin-ui/src/lib/mock/signalr.ts:26`). Calling `start()` twice on the same mock leaks the first interval because `stop()` can only clear the last stored handles (`src/admin-ui/src/lib/mock/signalr.ts:32`). The cleanup harness tests two different clients, not same-client double start (`src/admin-ui/integration/cleanup-harness.test.tsx:75`).

Recommended fix: Make mock `start()` return early when either timer is already set, and add a fake-timer test that same-client double start still emits one block per 20 seconds and leaves no timer active after stop.

### M6 - Bundle budget has too little headroom for S8-S10

Severity: MEDIUM
Slice: program-level

Issue: The S3 build leaves the shell at `198.19 kB gzip`, only `1.81 kB` under the `200 kB` A1 M3 ceiling. S8 is the first slice that will use MUI X DataGrid/Charts heavily; without route-level lazy boundaries there, S8 can breach the shell budget before S9/S10 hardening gets a chance to correct it.

Recommended fix: Require S8 to add route-level lazy loading for DataGrid/Charts screens immediately, not only S9/S10, and add a budget assertion that distinguishes shell from per-route chunks.

### L1 - One cleanup-harness assertion is weaker than its name

Severity: LOW
Slice: S3

Issue: The test "SignalR client stop() halts emit cadence" unsubscribes the handler immediately after `stop()` (`src/admin-ui/integration/cleanup-harness.test.tsx:57`). If `stop()` failed to clear the interval, that specific assertion would still pass because no handler remains attached (`src/admin-ui/integration/cleanup-harness.test.tsx:58`). The following "subscribe AFTER stop" test does cover this indirectly, so this is a harness sharpness issue rather than a product failure.

Recommended fix: Keep the subscription active after `stop()` in the cadence test and assert no additional events arrive, or combine the two cases so timer cleanup is proven directly.

### L2 - Contract followup doc is directionally useful but not executable yet

Severity: LOW
Slice: S3

Issue: `contracts/README.md` records the S3 deferral and expected paths, but the backend export step is still expressed as alternatives ("during `dotnet build` (or a one-shot `dotnet run --project ... -- --emit-swagger` task)") and the contract test directory contains only `.gitkeep` (`src/admin-ui/contracts/README.md:16`, `src/admin-ui/tests/contract/.gitkeep`). A future operator still has to choose the command shape.

Recommended fix: Replace the alternatives with the exact intended command/script names (`pnpm contracts:generate`, backend export command, expected `contracts/swagger.json`, expected `src/types/api.generated.ts`) and add a skipped/failing placeholder contract test that documents the launch condition.

### L3 - `pnpm verify` passes with lint warnings from unused disable comments

Severity: LOW
Slice: S3

Issue: ESLint reports unused `eslint-disable` directives in `src/lib/events/bus.ts:93` and `src/stores/pref.store.ts:105` during `pnpm verify`. This does not fail the gate, but it leaves noise in the foundational quality signal.

Recommended fix: Remove the unused disable comments or configure ESLint to treat unused disables consistently with the desired CI policy.

Verdict: MAJOR REVISION REQUIRED
Critical findings: 0
High findings: 2
Medium findings: 6
Low findings: 3
Headline: S3 builds and the primitive tests pass, but real SignalR is wired to the wrong backend route and the auth/logout error paths violate the foundational contract that S4-S10 depend on.
