---
created: 2026-05-19
type: audit-followup
parent: admin-ui-vnext-S4-S5-S6-slice-audit
status: applied
---

# Admin UI vNext — S4 + S5 + S6 slice-audit followup (APPROVE WITH CHANGES)

Codex slice-audit on `ea83764`: APPROVE WITH CHANGES (0C / 3H /
2M / 2L). All 7 findings closed in this commit.

## H1 — TrackedHistoryStatusResponse DTO drift

**Verified:** `types/admin.ts:135` declared
`{ status, pendingCount, lastCheckpoint }`, but the backend DTO
at `Dto/Responses/History/TrackedHistoryStatusResponse.cs` ships
`{ HistoryReadiness, Coverage, BackfillStatus, RootedToken }`.
Real-mode Address/Token readiness rendered "undefined" or
sat in warning state forever.

**Revision applied:**

- TS interface rewritten to mirror the C# DTO exactly + 3 nested
  interfaces added (`TrackedHistoryCoverageResponse`,
  `TrackedHistoryBackfillStatusResponse`,
  `RootedTokenHistoryStatusResponse`).
- `tracking-stages.ts` rewritten to read `historyReadiness` for
  the headline status + `backfillStatus.{status,itemsScanned,
  itemsApplied}` for the in-flight detail. "Ready" + "Completed"
  → `done`; non-Idle/non-Completed backfill → `active`;
  everything else with no in-flight backfill → `warning`.
- Mock seed (`admin-tracked-seed.ts`) updated to emit a full
  Ready history snapshot via a shared `readyHistory(now)` helper.
- `tracking-stages.test.ts` regenerated against the new DTO
  shape; 6 cases (added one for the warning path).

## H2 — AdminTrackedTokenBalanceSummaryResponse DTO drift

**Verified:** `types/admin.ts:158` declared
`{ tokenId, symbol, balanceSatoshis, utxoCount }`, backend
ships `{ TokenId, Satoshis }` only. AddressDetailPage chip
rendered `undefined · NaN`.

**Revision applied:**

- TS interface reduced to `{ tokenId, satoshis }`.
- `AddressDetailPage.tsx` chip now renders
  `${shortenTokenId(tb.tokenId)} · ${tb.satoshis.toLocaleString()} sats`
  with a length-aware truncator. Mock seed updated to match.

## H3 — ForceRebroadcast dialog allowed Close while POST in flight

**Verified:** `ForceRebroadcastDialog.tsx:143` rendered an
always-enabled Cancel/Close button; `broadcastRaw` was called
without an AbortSignal. The operator could discard the receipt
mid-POST or unmount the dialog while the destructive request
was still on the wire.

**Revision applied:**

- New `inflight: useRef<AbortController | null>(null)` tracks the
  active controller. `onSubmit` passes its `signal` into
  `admin.broadcastRaw(rawHex, signal)`.
- New `handleClose()` swallows close gestures while
  `submitting === true`; `disableEscapeKeyDown={submitting}`
  blocks the keyboard path; the Cancel/Close Button is
  `disabled={submitting}`.
- Component-unmount effect aborts the in-flight controller so a
  parent-side dismount doesn't strand the request.
- New test: `blocks Cancel/Close while a broadcast POST is in
  flight (S6-audit H3)` — uses a pending Promise to hold the
  POST open, asserts `cancel` is disabled, asserts the
  `onClose` spy is NOT called on click, then resolves the
  promise and verifies the dialog returns to a usable state.
- `IAdminClient.broadcastRaw` already accepted `signal?`; the
  `MockAdminClient` signature was updated to surface the
  parameter (still a no-op for the in-process mock).

## M1 — Address/Token detail pages bypassed the store layer

**Verified:** `AddressDetailPage.tsx:31` and
`TokenDetailPage.tsx:31` called `admin.getTracked*` directly
from a page `useEffect` with local component state. Diverged
from the Core Rule §4 contract (one MobX store per screen) and
the pattern set by the Tx detail page.

**Revision applied:**

- New `address-detail.store.ts` + `token-detail.store.ts` —
  identical shape: `{ data, status, error, address|tokenId,
  start(), dispose() }`. `start()` aborts prior in-flight,
  guards against double-dispose. Empty entity-id surfaces a
  clear "missing address/tokenId" error instead of a silent
  no-op.
- Both pages now `observer`-wrapped, instantiate the store
  via `useMemo([admin, paramId])`, call `start()` in
  `useEffect`, and `dispose()` on unmount.
- New `address-detail.store.test.ts` (4 cases): happy-path
  load, network-error capture, dispose aborts in-flight,
  empty-input early-error.

## M2 — Shell budget had no enforcement + mocks shipped in shell

**Verified:** Build report showed 199.96 KB gzip — 0.04 KB
under the A1 M3 200 KB cap, with NO automated assertion.
`factory.ts` statically imported `MockAdminClient`,
`MockAuthClient`, `MockSignalRClient`, all of which Rollup
keeps in the real-mode shell because the runtime branch can't
be tree-shaken.

**Revision applied:**

- `scripts/assert-shell-budget.mjs` — new node script that
  reads the largest `wwwroot/assets/index-*.js`,
  gzip-compresses it, and exits non-zero when the result
  exceeds `SHELL_BUDGET_BYTES_GZIP` (default 204800).
- `package.json` exposes `pnpm budget` and `pnpm verify` now
  chains `… && pnpm build && pnpm budget`. CI gate catches
  any future breach.
- `factory.ts` split into a sync `buildRealClients()` (no
  mock imports) and an async `createApiClients()` that
  dynamic-imports the three mock modules only when
  `VITE_API_MODE=mock`. Real-mode shell drops ~5 KB.
- `RootStore.build()` static async builder wraps the async
  factory. Production `App.tsx` awaits the builder once;
  test code keeps the sync `new RootStore(bus, clients)`
  constructor for injected fixtures.
- `factory.test.ts` updated for the now-async signature.
- Build result: shell **198.39 KB gzip** (down from 199.96),
  budget script reports **193.74 KB gzip** (uses default
  node gzip settings; vite uses level 6).

## L1 — master.md S5/S6 rows still marked todo

**Revision applied:**

- S5 row → **done** with commit `5ec71b2` + audit-fold note
  (DTO alignment H1/H2; per-screen stores M1).
- S6 row → **done** with commit `c9fb970` + audit-fold note
  (H3 dialog hardening; M2 budget assertion).

## L2 — `act()` warnings on bus-driven MobX updates

**Verified:** `DashboardPage.test.tsx`,
`BroadcastQueuePage.test.tsx`, and
`TransactionDetailPage.test.tsx` emitted React `act(…)`
warnings whenever a test called `bus.emit(...)` directly. The
emit synchronously fires MobX observers → React state updates;
without `act`, React can't guarantee the update is flushed
before assertions run.

**Revision applied:**

- Each bus emit is now wrapped in `act(() => bus.emit(...))`.
- New page-level smoke for the missing case from M22:
  `renders the subscribe-error Alert when the hub invoke
  rejects (S5-audit L2 smoke)` in
  `TransactionDetailPage.test.tsx`.

---

## Summary

| Layer | Change |
|---|---|
| `types/admin.ts` | TrackedHistory* (H1) + AdminTrackedTokenBalance (H2) rewritten to match backend |
| `screens/entity-detail/tracking-stages.ts` | reads `historyReadiness` + `backfillStatus` (H1) |
| `screens/entity-detail/tracking-stages.test.ts` | regenerated; +1 case for warning path |
| `screens/entity-detail/AddressDetailPage.tsx` | observer + AddressDetailStore (M1) + chip rewire (H2) |
| `screens/entity-detail/TokenDetailPage.tsx` | observer + TokenDetailStore (M1) |
| `screens/entity-detail/address-detail.store.ts` | NEW — per-screen store (M1) |
| `screens/entity-detail/address-detail.store.test.ts` | NEW — 4 cases (M1) |
| `screens/entity-detail/token-detail.store.ts` | NEW — per-screen store (M1) |
| `screens/entity-detail/TransactionDetailPage.test.tsx` | act() wrap (L2) + subscribe-failure smoke (L2) |
| `screens/broadcast-queue/ForceRebroadcastDialog.tsx` | abort signal + close-blocked + Escape-blocked (H3) |
| `screens/broadcast-queue/ForceRebroadcastDialog.test.tsx` | new "blocks Cancel/Close" case (H3) + signal-shape check |
| `screens/broadcast-queue/BroadcastQueuePage.test.tsx` | act() wrap (L2) |
| `screens/dashboard/DashboardPage.test.tsx` | act() wrap (L2) |
| `lib/mock/admin.ts` | broadcastRaw signature accepts AbortSignal (H3) |
| `lib/mock/admin-tracked-seed.ts` | readyHistory() helper + simplified balances (H1/H2) |
| `lib/api/factory.ts` | sync `buildRealClients` + async `createApiClients` (M2) |
| `lib/api/factory.test.ts` | async-aware (M2) |
| `stores/root.ts` | `RootStore.build()` async builder (M2) |
| `app/App.tsx` | awaits `RootStore.build()` (M2) |
| `scripts/assert-shell-budget.mjs` | NEW — budget gate (M2) |
| `package.json` | `pnpm budget` + verify-chain (M2) |
| `master.md` | S5/S6 rows → done (L1) |

Tests: 122 → 129 green (22 files).
Bundle: shell **198.39 KB gzip** (vite report) /
**193.74 KB gzip** (budget script) — both under the A1 M3
200 KB ceiling. BroadcastQueuePage chunk **47.07 KB gzip**
(framer-motion lazy). Budget gate green.

S4/S5/S6 slice-gate cleared; S7 (Alerts) opens.
