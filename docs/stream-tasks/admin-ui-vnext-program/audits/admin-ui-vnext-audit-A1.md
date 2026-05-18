# Admin UI vNext — Pre-execution Audit A1

Reviewer: GPT-5 Codex
Date: 2026-05-18
Audit target: `docs/stream-tasks/admin-ui-vnext-program/master.md` at `882ebc0`
Backend closeout baseline: `955e794`

## Scope

Audited the draft implementation program against:

- `docs/stream-tasks/admin-ui-vnext-program/master.md`
- `docs/admin-ui/design-handoff/00-design-brief.md`
- `docs/admin-ui/design-bundle/README.md`
- `docs/admin-ui/design-bundle/project/theme.jsx`
- `docs/admin-ui/design-bundle/project/Consigliere Admin.html`
- `/Users/imighty/Code/docs/project-stack-profiles.md`
- `/Users/imighty/Code/docs/frontend-principles.md`
- `/Users/imighty/Code/docs/frontend-audits-playbook.md`
- `docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`
- Actual repo state under `src/admin-ui/`, `src/Dxs.Consigliere/Controllers/`,
  `src/Dxs.Consigliere/WebSockets/`, and relevant DTO/source models.

## Checks That Passed

- Stack selection in the plan matches the default frontend baseline at the package
  level: React 19, Vite 7, TypeScript, MUI, MUI X DataGrid/Charts, MobX,
  `mobx-persist-store`, `react-router-dom` 7, `framer-motion`, and `pnpm`
  (`master.md:34-37`; `/Users/imighty/Code/docs/project-stack-profiles.md:22-31`).
- The slice ledger covers all 14 IA screens from the design brief:
  Dashboard S4; Transactions/Addresses/Tokens S5; Broadcast Queue S6; Alerts S7;
  P2P Pool S8; Source Metrics/Headers Chain/Broadcast Inspector S9;
  Configuration/Logs-Raw/Providers/Setup S10 (`design-brief.md:54-72`;
  `master.md:146-152`).
- Backend alert push is correctly declared out of scope at the product-decision
  level (`master.md:78-79`), matching the backend closeout's frozen SignalR
  inventory, which has block, reorg, and broadcast lifecycle events but no alert
  push (`closeout.md:79-85`).
- No C# source imports TypeScript files from `src/admin-ui/`; the only live
  backend reference is the publish-time project property `AdminUiRoot=..\admin-ui`
  (`src/Dxs.Consigliere/Dxs.Consigliere.csproj:46-57`).

## Verdict

Verdict: APPROVE WITH CHANGES
Critical findings: 0
High findings: 6
Medium findings: 10
Low findings: 5
Headline: The program is directionally sound, but S0 must fix routing, auth, CI, theme gating, contract parity, and shell ownership before implementation opens.

## Findings

### H1

- Severity: HIGH
- Slice: program-level
- Issue: `src/admin-ui/**` is routed to an out-of-catalog `admin-ui` zone
  (`master.md:130-136`), while repository rules require determining the zone by
  the zone catalog before planning. The catalog has no `src/admin-ui/**` entry
  (`docs/repository-zones/zone-catalog.md:5-14`), and the active CODEOWNERS
  template also has no admin UI mapping (`.github/CODEOWNERS.template:1-34`).
- Recommended fix: Before S0 opens, add `src/admin-ui/**` to
  `docs/repository-zones/zone-catalog.md`, `ownership-matrix.md`, and
  `.github/CODEOWNERS.template`, or explicitly map it to an existing catalog
  zone. Do not let child tasks use a non-catalog zone as routing truth.

### H2

- Severity: HIGH
- Slice: S0/S2/S3
- Issue: The plan omits admin auth/session and RBAC UX even though the default
  baseline requires RBAC in UX (`project-stack-profiles.md:47`) and actual
  admin endpoints are protected by `AdminAuthDefaults.Policy` (for example
  `AdminP2pController.cs:25-27`, `AdminMetricsController.cs:28-30`). The backend
  exposes cookie auth endpoints at `/api/admin/auth/me|login|logout`
  (`AdminAuthController.cs:13-75`).
- Recommended fix: Add auth/session as a foundational requirement: S0 creates
  auth scripts/tests, S2 owns route guards + login/setup/logout routes, and S3
  owns `me/login/logout` client methods, 401/403 handling, and cookie-mode e2e.

### H3

- Severity: HIGH
- Slice: S1/S2/S3
- Issue: Theme gating is internally inconsistent. Product Decision says S0 plus
  the foundational S1-S3 trio have slice-level audit gates (`master.md:41-44`),
  but Core Rule 11 and the ledger gate only S0, S2, and S3; S1 is marked
  `wave-A1` (`master.md:121-123`, `master.md:143`, `master.md:156-158`).
  S2 and all screen work depend on S1 tokens, density, and persisted palette.
- Recommended fix: Make S1 a slice-level audit gate, or explicitly state why
  theme/tokens can fail later without blocking S2/S3. Recommended DAG:
  `S0 -> S1 -> S2 -> S3 -> S4..S10 -> S11 -> S12`, with S1/S2/S3 all gated.

### H4

- Severity: HIGH
- Slice: S3
- Issue: Typed contracts are hand-written in `src/types/api.ts` or optionally
  generated (`master.md:108-110`), but the backend does not publish a checked-in
  OpenAPI/source package. The S3 parity test only promises mock-vs-real shape
  parity (`master.md:145`), not parity with C# DTOs such as
  `P2pAlertResponse/P2pAlertEventDto` (`AdminP2pController.cs:206-223`) or
  `BroadcastReceiptDto` (`BroadcastReceiptDto.cs:16-20`).
- Recommended fix: Add an explicit contract source in S3: generated Swagger JSON
  committed under `src/admin-ui/contracts/`, a codegen step, or a focused
  contract-parity test that boots the ASP.NET app and validates frontend schemas
  against real endpoint payloads. Keep mock parity as a separate test.

### H5

- Severity: HIGH
- Slice: S0
- Issue: CI does not know about the new Vite project. Current `ci-tests` only
  restores and tests the .NET solution (`.github/workflows/ci-tests.yml:19-28`),
  while the plan says S0 has CI gates and Core Rule 10 requires `pnpm verify` on
  every commit (`master.md:118-120`, `master.md:142`). The release Dockerfile
  builds the admin UI with pnpm (`Dockerfile:1-9`), so CI can miss failures that
  release builds hit.
- Recommended fix: Make S0 update CI: setup Node/corepack, run
  `cd src/admin-ui && pnpm install --frozen-lockfile && pnpm verify`, and keep
  backend `dotnet test`. Also add `lint`, `test`, and `verify` scripts to
  `src/admin-ui/package.json`.

### H6

- Severity: HIGH
- Slice: S2/S7
- Issue: S4-S10 are declared parallel after S3, but S7 owns the header alert
  badge (`master.md:149`) even though AppBar/header shell belongs to S2
  (`master.md:144`) and the design brief defines alert badge as a global header
  element (`design-brief.md:76-85`). This creates hidden shell edits in a screen
  slice and weakens the S4-S10 parallelization claim.
- Recommended fix: Move header badge, connection status, environment tag, and
  shell extension slots into S2. S7 should only feed alert-count/toast state
  through a store contract established in S3.

### M1

- Severity: MEDIUM
- Slice: S7
- Issue: The plan says SignalR push for alerts is out of scope (`master.md:78-79`)
  but S7 still says "new-alert toast" and "toasts appear on new fires"
  (`master.md:149`) without saying those fires are detected by
  `/api/admin/p2p/alerts?since=` polling. The design brief also contains a stale
  "SignalR everywhere ... alert fires" statement (`design-brief.md:195-199`)
  that is contradicted by its backend reference (`design-brief.md:473-479`).
- Recommended fix: Amend S7 to say alert toasts fire on poll-delta, with a
  dedupe cursor based on `AlertUnixMs`/`Id`, not SignalR push. Mark the
  design-brief real-time sentence stale for alerts.

### M2

- Severity: MEDIUM
- Slice: S3/S11
- Issue: Core Rule 6 requires cleanup, cancellation, and retry/backoff for every
  poll/timer/subscription (`master.md:105-107`), but no slice verifies leaks
  across route changes. The frontend playbook explicitly treats hydration/network
  race guards and refresh behavior as audit targets
  (`frontend-audits-playbook.md:85-96`).
- Recommended fix: Add S3 integration tests with fake timers and mocked SignalR
  proving route mount/unmount cancels pollers, removes subscriptions, aborts
  requests, and avoids duplicate listeners after repeated navigation.

### M3

- Severity: MEDIUM
- Slice: S11
- Issue: The initial JS budget is fixed at `<= 300 KB gzipped`
  (`master.md:153`, `master.md:167-168`) while the mandated stack includes MUI
  Material, icons, DataGrid, and X Charts (`project-stack-profiles.md:22-31`).
  The plan does not require route-level lazy boundaries for DataGrid/Charts-heavy
  system screens.
- Recommended fix: Either raise the budget to a measured MUI baseline or require
  code splitting in S2/S8/S9/S10: shell-only initial bundle budget plus per-route
  chunk budgets for DataGrid and X Charts screens.

### M4

- Severity: MEDIUM
- Slice: S0/S2
- Issue: The plan mentions unit, integration, and e2e concepts indirectly but
  does not lock the testing pyramid or file locations. Frontend Principle 18
  requires a clear testing pyramid (`frontend-principles.md:28`), and the
  playbook separates contract drift, mock reliability, hydration/network, list,
  detail, performance, a11y, and mobile audits (`frontend-audits-playbook.md:74-219`).
- Recommended fix: Add a S0 testing-standard section: unit = component/store
  render and state transitions; integration = store + API/mock/route hydration;
  e2e = Playwright golden paths; contract = backend payload/schema parity.

### M5

- Severity: MEDIUM
- Slice: S11
- Issue: The plan claims all screens are responsive/mobile by implication, but
  the design bundle only provides Dashboard mobile at 375px (`Consigliere Admin.html:63-68`).
  The design brief explicitly says screens 6-14 are tokens/patterns only, not
  per-screen hi-fi mockups (`design-brief.md:448-453`).
- Recommended fix: Record this as a design-handoff residual and add a mobile QA
  matrix in S11 for all 14 screens at least at 375px and one narrow desktop-ish
  breakpoint, covering DataGrid overflow, drawer behavior, dialogs, and snackbars.

### M6

- Severity: MEDIUM
- Slice: S2/S5/S9
- Issue: Smart search routing is under-specified in the ledger. S2 says search
  resolves by format and routes (`master.md:144`), while the design brief pins
  ambiguity behavior: 64-hex resolves tx first, falls back to block hash, and
  ambiguous/not-found cases render a "did you mean" panel (`design-brief.md:205-215`).
- Recommended fix: Add a search grammar/decision table and tests in S2,
  including 64-hex tx-vs-block ambiguity, integer height, address, token-id, and
  not-found candidates. Assign the candidate panel to S2 or the relevant detail
  slice explicitly.

### M7

- Severity: MEDIUM
- Slice: S3/S4-S10
- Issue: "One MobX store per screen" is sound (`master.md:99-101`), but the plan
  does not define how cross-screen invalidation works when SignalR block tips,
  reorgs, or broadcast state changes arrive. `OnNewBlock`, `OnReorg`, and
  `OnBroadcastStateChanged` are real hub events (`IWalletHub.cs:18-30`).
- Recommended fix: Add a concrete event-bus or root-store publish/subscribe
  pattern in S3, including cache keys, stale markers, and which stores react to
  block, reorg, and broadcast lifecycle events.

### M8

- Severity: MEDIUM
- Slice: S10
- Issue: Configuration is read-only this wave, but S10 still requires a
  feature-flagged tune affordance that is present and off (`master.md:152`).
  The backend closeout lists score-weight tuning/runtime config as a residual
  requiring future backend work (`closeout.md:145-149`).
- Recommended fix: Remove the hidden tune affordance from this wave. Ship a
  read-only Configuration screen plus documented future UX requirements; add the
  edit affordance only when the backend hot-reload/runtime-config endpoint exists.

### M9

- Severity: MEDIUM
- Slice: S0/S3/S11
- Issue: Frontend Principles 14 and 15 are not materially covered: no client-side
  security hygiene rule for avoiding sensitive logs/secrets and no observability
  or key UX event telemetry (`frontend-principles.md:24-25`). This is an admin
  UI handling raw hex, config keys, auth state, and operational data.
- Recommended fix: Add rules in S0/S3 for redaction, no secrets in client state,
  no credentials/raw sensitive payloads in logs, sanitized raw JSON rendering,
  and lightweight structured UI error/event telemetry.

### M10

- Severity: MEDIUM
- Slice: S8/S11
- Issue: The score gradient is only covered by the global S11 a11y pass
  (`master.md:153`), but P2P Pool owns the gradient (`theme.jsx:20-21`,
  `theme.jsx:37`) and is the only screen where this risk is introduced. Waiting
  until S11 can make the S8 layout visually accepted before contrast is proven.
- Recommended fix: Add an S8 acceptance check for score gradient contrast in
  both palettes, including text/icon overlays and non-color indicators for the
  score bucket. S11 should re-run the whole-app axe/Lighthouse audit.

### L1

- Severity: LOW
- Slice: S1
- Issue: Theme/density persistence is required in S1 (`master.md:143`), but
  there is no persisted-snapshot versioning or migration convention for future
  token renames. The persistence audit checklist explicitly asks for reset and
  migration rules (`frontend-audits-playbook.md:97-106`).
- Recommended fix: Add a `persistVersion` convention in S1 and document reset
  behavior for unknown/old theme or density snapshots.

### L2

- Severity: LOW
- Slice: program-level
- Issue: Product Decision says "Workspace-default stack, no deviations"
  (`master.md:34-37`), while the Definition of Done accepts two design-bundle
  deviations: `shape.borderRadius: 8` and `typography.code: JetBrains Mono`
  (`master.md:177-179`). The bundle itself flags those as intentional
  (`Consigliere Admin.html:125-130`; `theme.jsx:48-69`).
- Recommended fix: Clarify Product Decision as "no stack deviations"; list the
  two accepted design-token deviations there, not only in closeout DoD.

### L3

- Severity: LOW
- Slice: S12
- Issue: Closeout requirements are thin: delivery hashes, residuals, and a
  screenshot (`master.md:174-176`), plus accepted design deviations
  (`master.md:177-179`). The frontend audit playbook expects audit output to
  include scope, sources of truth, findings, risks, remediation order, and open
  assumptions (`frontend-audits-playbook.md:279-286`).
- Recommended fix: Expand S12 closeout sections: delivery hashes per slice,
  validation commands, CI evidence, before/after screenshots, contract-parity
  evidence, mobile/a11y/perf reports, design deviations, residuals, and open
  assumptions.

### L4

- Severity: LOW
- Slice: S0
- Issue: Deleting/replacing `src/admin-ui/` is mostly safe from C# imports, but
  the backend publish target still assumes `..\admin-ui\package.json` and runs
  `pnpm install`/`pnpm build` when present
  (`Dxs.Consigliere.csproj:46-57`). A partial S0 deletion before scaffold
  completion would silently skip admin UI publish.
- Recommended fix: Keep S0 atomic: delete the legacy stub and add the new
  `src/admin-ui/package.json` in the same slice. Add an S0 check that
  `dotnet publish -p:SkipAdminUiBuild=false` either builds the UI or fails
  loudly if the admin UI package is missing.

### L5

- Severity: LOW
- Slice: program-level
- Issue: The audit cadence is heavier than the frontend playbook requires for a
  single UI program: product decision says every slice has pre/post audits
  (`master.md:41-44`), while Core Rule 11 says S4-S10 are covered by program A1
  plus normal per-slice review (`master.md:121-123`). This is both a cost issue
  and a documentation contradiction.
- Recommended fix: Pick one cadence. Recommended: slice-level audits for
  S0-S3, normal reviews for S4-S10, S11 whole-app quality audit, and S12 A2
  closeout audit. Optionally batch similar operator screens S5-S7 under one
  audited execution block if implementation capacity is constrained.
