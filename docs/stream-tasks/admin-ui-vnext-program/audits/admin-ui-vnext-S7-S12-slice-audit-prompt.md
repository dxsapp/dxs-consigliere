# Admin UI vNext — S7-S12 + closeout audit prompt

Audit target: the system-screens wave + polish + closeout
(S7–S12) at HEAD `0b89bc6` on `codex/consigliere-vnext`. Run
sync; verdict closes wave-A1.

---

You are auditing the **system-screens wave + polish + closeout**
for the new admin UI. S0–S6 have signed-off slice audits; this
audit covers the remaining six slices in one pass.

Read first:

- `docs/stream-tasks/admin-ui-vnext-program/master.md` (the
  approved plan; S7-S12 rows now marked done with summaries)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S4-S5-S6-slice-audit-followup.md`
  (the immediate-upstream audit fold — esp. M2 budget gate, H3
  abort signal, H1 DTO mirror discipline)
- `docs/stream-tasks/admin-ui-vnext-program/evidence/closeout.md`
  (the S12 closeout claim — every assertion here must verify
  against the actual code at HEAD)
- `docs/stream-tasks/admin-ui-vnext-program/evidence/s11-polish-notes.md`
- `/Users/imighty/Code/docs/frontend-principles.md` §6 (cleanup),
  §8 (error normalization), §10–11 (MobX store discipline),
  §16-17 (route-driven hydration)
- Backend reference (consume-only — no edits expected):
  - `src/Dxs.Consigliere/Controllers/AdminP2pController.cs`
    (`/api/admin/p2p/{health,peers,headers/tip,headers/recent,alerts}`)
  - `src/Dxs.Consigliere/Data/Models/P2p/P2pAlertEvent.cs`
    (the 4-value `P2pAlertType` enum)
  - `src/Dxs.Consigliere/Controllers/AdminProvidersController.cs`
  - `src/Dxs.Consigliere/Dto/Responses/Admin/AdminProviders*`
  - `src/Dxs.Consigliere/Controllers/AdminMetricsController.cs`
  - `src/Dxs.Consigliere/Controllers/SetupController.cs`
  - `src/Dxs.Consigliere/Dto/Responses/Setup/SetupStatusResponse.cs`

Cross-validate against the deliverables on the six commits:

S7 (`93ffbd3`)
- `types/admin.ts` — P2pAlertType + EventDto + Response, peer +
  headers DTOs, AdminProviders* + SetupStatusResponse
- `lib/api/routes.ts` — adminAlertsPath + peers + headers + providers + setup
- `lib/admin/admin-client.ts` — getAlerts / getPeers /
  getHeadersTip (404→null) / getHeadersRecent / getProviders /
  getSetupStatus
- `lib/mock/admin.ts` + `lib/mock/admin-systems-seed.ts` (lazy)
- `screens/alerts/{alerts.store,AlertsPage,AlertHistoryGrid}.*`
  + `alerts.store.test.ts` (7) + `AlertsPage.test.tsx` (2)

S8 (`9535555`)
- `screens/p2p/{p2p.store,P2pPage,ScoreBar,SubnetDonut}.*`
- `p2p.store.test.ts` (4)

S9 (`9531c29`)
- `screens/source-metrics/{source-metrics.store,SourceMetricsPage,SourceMetricsCharts}.*`
- `screens/headers/{headers.store,HeadersPage}.*`
- `screens/broadcast-inspector/BroadcastInspectorPage.tsx`
- `source-metrics.store.test.ts` (2) + `headers.store.test.ts` (3)

S10 (`56d1a19`)
- `screens/configuration/ConfigurationPage.tsx`
- `screens/providers/ProvidersPage.tsx`
- `screens/logs/LogsPage.tsx` + `LogsPage.test.tsx` (6)
- `screens/setup/SetupPage.tsx`

S11 (`dd663ee`)
- `tests/a11y/shell-a11y.test.tsx`
- `scripts/bundle-inventory.mjs` + `pnpm inventory`
- `package.json` verify-chain extension
- `docs/.../evidence/s11-polish-notes.md`

S12 (`0b89bc6`)
- `playwright.config.ts` + 3 specs in `tests/e2e/`
- `docs/.../evidence/closeout.md`
- `master.md` S7-S12 row updates

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

### DTO + contract parity

1. **Alerts DTO parity (S7).** `P2pAlertEventDto` matches the
   backend record verbatim (id / alertUnixMs / type / detail /
   context). `P2pAlertResponse.alerts` shape matches the
   controller's frozen `P2pAlertResponse(IReadOnlyList<...>)`.
   The TS `P2pAlertType` constant covers all 4 enum members
   from `P2pAlertEvent.cs`.
2. **Peers DTO drift risk (S8).** The peers endpoint returns
   `total / successful / failed / distinctSubnets / peers[]`
   with the named row fields. Verify the TS mirror in
   `types/admin.ts` exactly matches (in particular the date
   fields — backend returns `DateTime?` which serializes as ISO
   strings or null; TS uses `string | null`).
3. **Providers DTO depth (S10).** Full `AdminProvidersResponse`
   shape across recommendations, config (static/override/effective
   triplet + restartRequired + the allowed-* lists + updatedAt/By),
   and the catalog item with helpLinks. Spot-check at least one
   missing/extra field per nested DTO.
4. **Setup DTO shape (S10).** `SetupStatusResponse` matches the
   sealed C# class verbatim.
5. **Frozen contracts respected.** No screen mutates
   `BroadcastReceiptDto`, `P2pHealthDto`, `HeadersTipDto`,
   `P2pAlertEventDto`. Master.md handoff table marks several as
   frozen; flag any rename-without-amendment.

### Live-data pipelines

6. **AlertsStore poll-delta correctness (S7 / A1 M1).**
   - `?since=` cursor advances on every merge and seeds the
     next call (test pin: `advances the cursor on the first poll
     + uses it on the next`).
   - id-based dedupe prevents the same alert appearing twice on
     overlapping pages.
   - `consumeNewAlerts()` drains the queue so toasts don't
     double-fire on rerender.
   - The poll is NOT a SignalR subscription (A1 M1 invariant).
7. **P2pStore score derivation (S8).** Composite =
   `0.5*accept + 0.3*recency + 0.2*diversity`. Verify the
   recency function decays linearly from 1 (lastSeen now) to 0
   (24h+ ago); diversity is binary (1 iff peer's /24 is
   otherwise unrepresented). Both halves are integer-rounded to
   composite ∈ [0, 100].
8. **HeadersStore + bus.OnReorg (S9).** Subscribes to OnReorg in
   `start()`, captures the event into `reorgEvents` with the
   observedAt timestamp, caps at 50 entries. `dispose()`
   unwires the subscription.
9. **BroadcastInspector reuse (S9).** The page constructs a
   `TransactionDetailStore` only after the receipt arrives
   (txId-keyed `useMemo`); the prior store is disposed when
   the txId changes. No double-subscribe to the bus.
10. **SourceMetricsStore deltas (S9).** `firstSeenSeries(src)`
    returns `[]` for fewer than two snapshots; otherwise emits
    the per-tick delta, clamped to non-negative. Matches the
    Dashboard's `mempoolRateSeries` semantics for parity.

### Destructive-action gating

11. **No new destructive surfaces (S7-S10).** Only
    `ForceRebroadcastDialog` (S6) is destructive. Spot-check
    that no S7-S10 page exposes a write surface — Configuration,
    Providers, Logs, Setup are all read-only per design brief §7.
12. **Broadcast Inspector + Force-rebroadcast dialog**. Both
    POST to `/api/tx/broadcast`. The inspector does NOT include
    the S6-audit H3 two-step confirm (it's a "submit + watch"
    investigation tool, not a stale-card retry). Flag this only
    if the design brief explicitly required the same gating
    here.

### Cleanup discipline (Core Rule §6)

13. **Per-screen dispose contracts.** For each S7-S10 store, prove
    via test or code-read that `dispose()`:
    - clears every timer (poll interval; alerts/p2p/headers/
      source-metrics; broadcast-queue sweep already pinned in S6)
    - aborts every in-flight request
    - unwires every bus subscription (headers OnReorg)
    - is idempotent under StrictMode double-mount
14. **Lazy-suspense boundaries.** Each lazy import in App.tsx is
    behind a `<Suspense fallback={null}>` so a slow chunk fetch
    doesn't crash the parent. Confirm for the four new S10 pages
    + the S9 charts wrapper inside SourceMetricsPage.

### MobX correctness

15. **`makeAutoObservable` + autoBind.** Every new store uses
    `makeAutoObservable(this, {}, { autoBind: true })`. Flag any
    deviation.
16. **Computed isolation.** Per-source computed getters
    (`activeAlerts`, `historyAlerts`, `scoredPeers`,
    `subnetBreakdown`, `firstSeenSeries`) don't reach for
    `Date.now()` directly inside the getter — they go through
    the injected `nowFn` so tests are deterministic.

### Bundle + perf

17. **Per-route inventory (S11).** `pnpm inventory` enforces the
    default 256 KB gzip per-route ceiling. Verify the script's
    glob actually picks every `wwwroot/assets/*.js` chunk and the
    "shell = largest index-*.js" heuristic still holds after
    Vite's recent chunking changes (currently true: SignalR
    stub is the smaller `index-*.js`).
18. **DataGrid chunk reuse (S7 + S8).** Both screens import
    `@mui/x-data-grid`; Vite shares the chunk. The current
    DataGrid chunk is ~128 KB gzip — verify it's a single chunk
    (not duplicated per route).
19. **X-Charts ChartsWrapper shared (S8 + S9).** Same check —
    PieChart (S8 donut) + LineChart (S9 source-metrics charts)
    must share the `ChartsWrapper` chunk.

### Sanitizer (S10)

20. **LogsPage sanitizer rule coverage.** Verify each rule against
    a realistic example:
    - Authorization headers: `Authorization: Bearer xyz`
    - Cookie headers: `Cookie: session=...`
    - WIF keys: 51-52 char base58 strings starting with 5/K/L
    - JSON `apiKey` fields (single + double quoted)
    - 128+ hex blobs
    Flag any rule that misses legit input OR over-redacts
    benign text (false positive on log noise).
21. **Sanitizer evaluation order.** The hex-blob rule runs LAST
    so an apiKey value that's hex doesn't get partially redacted
    twice. Verify the current order in `SANITIZE_RULES`.

### A11y + mobile

22. **Shell invariants (S11).** Every header IconButton has an
    `aria-label`. Drawer renders the operator routes as links
    on every breakpoint. Tooltips on the alerts/theme/logout
    buttons describe the count + the current state.
23. **Snackbar lifecycle (S7).** Toasts cap at 3 in-flight, 8s
    autoHide. The page's drain effect doesn't re-fire on every
    rerender (otherwise toasts would multiply).
24. **EntityTimeline reuse (S9).** Broadcast Inspector passes
    `store.stagesView` into the shared Stepper, satisfying the
    "single timeline component" master.md commitment.

### E2E + closeout (S12)

25. **Playwright config sanity.** `webServer` boots `pnpm dev`
    with `VITE_API_MODE=mock`. Mobile project = Pixel 5. The
    base URL is overridable via `E2E_BASE_URL`. CI workflow
    needs `pnpm test:e2e:install` step before `pnpm test:e2e`.
26. **Spec coverage.** Three specs cover the master.md S12 done-
    when: login/search/Tx, Force-rebroadcast confirmation,
    Alerts toast. The "Alerts toast on injected poll-delta"
    case is currently the static active-card render (not the
    toast); flag if the toast spec is missing.
27. **Closeout doc completeness.** `evidence/closeout.md`
    follows the playbook template. Verify each section is
    populated AND that the per-slice commit hashes resolve.
    Spot-check the residuals list: swagger codegen, axe-core
    sweep, backend log streaming, read-write Configuration.
28. **Master ledger freshness.** Every S7-S12 row is marked done
    with a meaningful summary (not just "shipped"); the bundle
    figures cited match the current build report.

### Doc + plan + invariants

29. **No backend touched.** `git diff src/Dxs.Consigliere/**`
    over the six commits MUST be empty (Core Rule §10 + master
    program scope).
30. **No legacy admin app references.** Spot-check that none of
    the new screens reach into `wwwroot/legacy-admin/**` or
    import from there.

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
- Slice (S7 / S8 / S9 / S10 / S11 / S12 / program-level)
- Issue (1-2 sentences with `file:line` where applicable)
- Recommended fix (concrete)
