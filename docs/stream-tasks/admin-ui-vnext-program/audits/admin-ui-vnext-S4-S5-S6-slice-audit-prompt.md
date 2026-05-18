# Admin UI vNext — S4 + S5 + S6 slice-audit prompt

Audit target: the operator-screen wave (S4 Dashboard, S5 Entity
detail, S6 Broadcast Queue) at HEAD `c9fb970` on
`codex/consigliere-vnext`. Run sync; this gates S7+ open.

---

You are auditing the **operator-screen wave**. S0–S3 are the
audited foundation (auth, event bus, SignalR, mock layer, theme,
shell, routing); these three slices are the first end-user screens
to consume that foundation and the first to expose a destructive
action (Force rebroadcast).

Read first:

- `docs/stream-tasks/admin-ui-vnext-program/master.md`
  (the approved plan; S4/S5/S6 rows + Core Rules §1–§15)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S3-slice-audit-followup.md`
  (the upstream contract — esp. M1 `connected` flag, M3 idempotent
  hydrate, M4 `failReason: string | null`, M5 idempotent mock
  signalr, M6 shell budget warning)
- `/Users/imighty/Code/docs/frontend-principles.md` §6
  (cleanup), §8 (error normalization), §10–11 (MobX
  store-per-screen + observability), §16-17 (route-driven
  hydration)
- Backend reference (consume-only — no edits expected):
  - `src/Dxs.Consigliere/Controllers/AdminP2pController.cs`
    (`/api/admin/p2p/health`)
  - `src/Dxs.Consigliere/Controllers/AdminMetricsController.cs`
    + `Data/Models/Metrics/SourceMetricsSnapshot.cs`
  - `src/Dxs.Consigliere/Controllers/AdminTrackedController.cs`
    + `Dto/Responses/Admin/AdminTracked{Address,Token}Response.cs`
    + `Dto/Responses/Readiness/TrackedEntityReadinessResponse.cs`
  - `src/Dxs.Consigliere/Controllers/TransactionController.cs`
    (`POST /api/tx/broadcast` + `BroadcastTxRequest`
    + `WebSockets/BroadcastReceiptDto`)
  - `src/Dxs.Consigliere/Data/Models/P2p/OutgoingTxState.cs`

Cross-validate against the wave's deliverables (commits 751f117 ·
5ec71b2 · c9fb970):

S4 — Dashboard

- `src/admin-ui/src/types/admin.ts` (S4 portion: P2pHealthDto,
  Source*, BlockTipDto)
- `src/admin-ui/src/lib/admin/admin-client.ts` (S4 methods)
- `src/admin-ui/src/lib/mock/admin.ts` (S4 seed)
- `src/admin-ui/src/screens/dashboard/dashboard.store.ts`
  + `dashboard.store.test.ts` (11 cases)
- `src/admin-ui/src/screens/dashboard/DashboardPage.tsx`
  + `DashboardPage.test.tsx` (4 cases)
- `src/admin-ui/src/screens/dashboard/components/Sparkline.tsx`

S5 — Entity detail

- `src/admin-ui/src/types/admin.ts` (S5 extensions:
  OutgoingTxState + helpers, AdminTracked* + readiness)
- `src/admin-ui/src/lib/api/routes.ts` (path builders + encoding)
- `src/admin-ui/src/lib/admin/admin-client.ts` (S5 methods)
- `src/admin-ui/src/lib/mock/admin.ts`
  + `src/admin-ui/src/lib/mock/admin-tracked-seed.ts` (lazy
    fixtures behind dynamic import per S3-audit M6)
- `src/admin-ui/src/screens/entity-detail/EntityTimeline.tsx`
- `src/admin-ui/src/screens/entity-detail/tracking-stages.ts`
  + `tracking-stages.test.ts` (5 cases)
- `src/admin-ui/src/screens/entity-detail/transaction-detail.store.ts`
  + `transaction-detail.store.test.ts` (8 cases)
- `src/admin-ui/src/screens/entity-detail/TransactionDetailPage.tsx`
  + `TransactionDetailPage.test.tsx` (2 cases)
- `src/admin-ui/src/screens/entity-detail/AddressDetailPage.tsx`
  + `TokenDetailPage.tsx`
  + `AddressDetailPage.test.tsx` (2 cases)

S6 — Broadcast Queue

- `src/admin-ui/src/types/admin.ts` (`BroadcastReceiptDto`)
- `src/admin-ui/src/lib/api/routes.ts` (`TX_BROADCAST_PATH`)
- `src/admin-ui/src/lib/admin/admin-client.ts` (`broadcastRaw`)
- `src/admin-ui/src/lib/mock/admin.ts` (mock broadcast + hexFold)
- `src/admin-ui/src/screens/broadcast-queue/broadcast-queue.store.ts`
  + `broadcast-queue.store.test.ts` (9 cases)
- `src/admin-ui/src/screens/broadcast-queue/BroadcastQueuePage.tsx`
  + `BroadcastQueuePage.test.tsx` (3 cases)
- `src/admin-ui/src/screens/broadcast-queue/ForceRebroadcastDialog.tsx`
  + `ForceRebroadcastDialog.test.tsx` (3 cases)
- `src/admin-ui/src/app/App.tsx` (route wiring for all three)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

### DTO + contract parity

1. **Admin DTO parity (S4 + S5).** Verify each TS interface in
   `types/admin.ts` is field-for-field accurate against the C#
   source (names, types, nullability). Particular care:
   `TrackedEntityReadinessResponse.history` is `null`-able;
   `*SummaryResponse.tokenBalances` is `[]` by default in C#;
   `BroadcastReceiptDto.failReason` is `string = null`
   (serialized null literal, not absent — see S3-audit M4).
2. **OutgoingTxState mirror (S5).** All 14 enum members in
   `Data/Models/P2p/OutgoingTxState.cs` are reflected in the TS
   union. The 5-stage happy-path projection is operator-correct
   (Validated → Dispatching → PeerRelayed → Mined → Confirmed)
   and the failure-state map is sensible (PolicyInvalid pinned
   to Validated stage, ConflictRejected to Dispatching, etc.).
   Flag any state mis-classification.
3. **Broadcast contract (S6).** `POST /api/tx/broadcast` body
   shape `{ rawHex }` matches `BroadcastTxRequest`. Response
   parse matches `BroadcastReceiptDto` (no extra fields, no
   missing ones).

### Live-data pipelines

4. **Dashboard hydration.** `DashboardStore.start()` polls
   health (15s) + metrics (30s) and subscribes to
   `OnBroadcastStateChanged`. Verify:
   - Idempotent under StrictMode double-mount.
   - `refreshHealth`/`refreshMetrics` abort prior in-flight
     requests; per-slice error fields prevent cross-pollution.
   - `dispose()` clears both timers + bus subscription + aborts
     pending requests.
   - The LRU broadcast list correctly merges by txid (newest
     wins) and caps at 12.
5. **TransactionDetailStore live wiring (S5).**
   - `start()` subscribes to bus by `txId` filter + invokes
     `signalR.subscribeToBroadcast(txId)`; failure is captured
     in `subscribeError` but bus events still flow.
   - `start()` is idempotent.
   - Event dedupe is by `(state, updatedAtMs)`. Flag if a
     duplicate hub emit could double-stamp the timeline.
   - The Stepper status projection handles failure (failed
     stage = state's mapped lane), terminal happy-path
     (Confirmed = done), and mid-stream re-entries.
6. **BroadcastQueueStore lane projection (S6).**
   - `LANE_BY_STATE` table is correct: PeerAcked → Dispatching;
     MempoolSeen → PeerRelayed. Terminal states keep the card
     on the prior lane until linger expires.
   - `enteredLaneAtMs` only resets when the lane changes (not on
     intra-lane state churn) — verify the stale clock isn't
     reset by repeated PeerAcked events.
   - `isStale(card)` reads `staleTick` to force MobX rerender
     after a sweep — verify this is observable-correct (not a
     `void this.staleTick` no-op that the optimiser drops).
   - The 15s sweep timer is wired in `start()` and cleared in
     `dispose()`; tests can drive `forceSweep()` directly.
   - Terminal event for an unknown txid is silently dropped.

### Destructive action

7. **Force-rebroadcast two-step (S6).** The dialog gates the
   POST behind a second click ("Confirm broadcast"). Verify:
   - `isPlausibleRaw` rejects odd-length, non-hex, < 20 chars.
     Flag if it's too tight (legit small txs would be ≥ ~60
     chars anyway, but the rule should not block valid input).
   - `submitting` flag disables both buttons + the textarea.
   - Network error renders an inline Alert; the dialog stays
     open so the operator can retry with a different rawHex.
   - On success, the textarea is locked + the receipt is
     rendered; "Close" returns to a clean state on next open.
   - The dialog cannot accidentally trigger TWO concurrent
     POSTs from rapid double-click (race-window check).
8. **Force-rebroadcast surface.** Is the per-card "Retry"
   button + the page-level CTA the right surface? The design
   brief §7 says the dialog is the only destructive
   affordance — verify there is no other code path that calls
   `admin.broadcastRaw()` without the dialog confirmation.

### Cleanup discipline (Core Rule §6)

9. **Dashboard / TransactionDetail / BroadcastQueue cleanup.**
   For each screen store, prove via test or inspection that:
   - Bus subscription is unwired in `dispose()`.
   - All timers (intervals + setTimeout) are cleared.
   - All AbortControllers are aborted.
   - Calling `dispose()` then emitting a bus event is a no-op.
   - Calling `dispose()` then re-using the store does NOT
     leak listeners or re-arm timers without an explicit
     `start()`.
10. **Page lifecycle.** Each `*Page` component creates the
    store in a `useMemo`(admin, bus, signalR), calls `start()`
    in `useEffect`, and disposes on unmount. Verify:
    - The useMemo deps cover the right inputs (changing the
      txid param creates a fresh store).
    - There is no chance of "ghost store" where unmount races
      a still-pending `start()` async resolution.

### MobX correctness

11. **`makeAutoObservable` annotations.** Each store uses
    `makeAutoObservable(this, {}, { autoBind: true })`. Flag if
    private fields (`disposers`, `cards` Map, `sweepTimer`)
    cause issues with auto-observability or autoBind.
12. **Map field observability (S6).** `BroadcastQueueStore.cards`
    is a `Map`. Verify MobX wraps it as an observable map +
    that the getters (`validated`/`dispatching`/`peerRelayed`)
    correctly track changes. Confirm `Array.from(map.values())`
    + filter + sort triggers recomputation on map mutation.
13. **`staleTick` rerender trigger.** The `void this.staleTick`
    expression in `byLane` + `isStale` — does it actually
    force recompute? Confirm via test or rationale; otherwise
    propose a more direct trigger (e.g. assignment of the tick
    inside the computed read, or moving sweep state into a
    plain observable that the UI reads).

### Bundle headroom

14. **S6 bundle envelope.** BroadcastQueuePage chunk is
    46.96 KB gzip (framer-motion bundled). Verify:
    - The framer-motion is NOT also in the shell.
    - The chunk is lazy-loaded via `lazy(...)` in App.tsx.
    - Are there cheaper imports from framer-motion (e.g.
      `motion/dom` vs `framer-motion`) that would shrink the
      chunk? Note that S3-audit M6 required per-route chunks
      to be reported separately — flag if 47 KB feels high
      for what's effectively layout + AnimatePresence.
15. **Shell ceiling.** 199.96 KB gzip — 0.04 KB under the A1
    M3 200 KB cap. Flag whether the dashboard-store + admin
    type constants leak unnecessary code into the shell (e.g.
    `TX_HAPPY_PATH` is consumed only by transaction-detail
    and broadcast-queue; if it's tree-shaken into the shell
    that's a bug).

### Cross-cutting

16. **Route encoding.** `adminTrackedAddressPath` +
    `adminTrackedTokenPath` use `encodeURIComponent`. Verify
    that for the address types BSV uses (legacy/base58, taproot
    if relevant), this never produces a path the backend
    rejects. For `tokenId` containing `:` or `_`, confirm
    server-side parse still works.
17. **`useParams` undefined handling.** S5 pages default to
    `""` if `txid`/`address`/`tokenId` is missing. Verify:
    - Empty input doesn't trigger a request (Address/Token
      pages skip the fetch on falsy input).
    - The "(missing)" placeholder is rendered consistently.
18. **Search-bar entry points.** Pasting a 64-char hex into
    the header search should route to `/transactions/:txid`.
    Verify the grammar + entityToPath still hit the new pages
    correctly (S2 deliverable).
19. **Stepper accessibility (S5).** `EntityTimeline` renders
    a vertical Stepper with status icons. Verify:
    - The status is exposed via `data-status` for tests AND
      via a meaningful `aria-label` or visible label.
    - Failed/warning/unknown icons have sufficient colour
      contrast (theme-demo regression baseline still passes).
20. **Mock parity.** `MockAdminClient`:
    - All seed methods return shapes that the real backend
      could plausibly emit (no future-only fields).
    - `broadcastRaw`'s `hexFold` produces a deterministic
      64-hex-char id — verify it's stable + lower-case (BSV
      txids are lower-case hex on the wire).

### Tests

21. **Test sharpness.** Spot-check 3-5 tests for "tests the
    behaviour, not the implementation":
    - `broadcast-queue.store.test.ts > retains a terminal card
      until linger expires then drops it` — does it actually
      prove the linger window, or just that sweep removes
      terminals?
    - `ForceRebroadcastDialog.test.tsx > two-step confirms
      before POSTing` — does the assertion sequence prevent a
      regression where the first click POSTs directly?
    - `transaction-detail.store.test.ts > advances the
      happy-path stages` — does it cover every transition,
      including the operator-collapsed states (PeerAcked,
      MempoolSeen)?
22. **Test coverage gap.** Flag missing tests:
    - No test for the SignalR `subscribeToBroadcast` invoke
      failure case on the Transaction page (the store has it;
      the page renders an Alert — is that smoke-tested?)
    - No test that `dispose()` cancels in-flight `broadcastRaw`.
    - No test that the dashboard sparkline handles the
      single-snapshot case (no delta computable).

### Doc + plan

23. **Master.md row freshness.** S4/S5/S6 rows should reflect
    "shipped" status with the commit hash. Verify the
    done-when checklist matches what's actually in the code.
24. **No backend touched.** `git diff` over
    `src/Dxs.Consigliere/**` for the three commits must be
    empty (consume-only is a Core Rule).
25. **No legacy admin app references.** Spot-check that the
    new screens don't accidentally reach into the legacy
    `wwwroot/legacy-admin/**` or import from there.

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
- Slice (S4 / S5 / S6 / program-level)
- Issue (1-2 sentences with `file:line` where applicable)
- Recommended fix (concrete)
