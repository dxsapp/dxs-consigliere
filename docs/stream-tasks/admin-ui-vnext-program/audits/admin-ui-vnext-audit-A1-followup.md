---
created: 2026-05-18
type: audit-followup
parent: admin-ui-vnext-audit-A1
status: applied
---

# Admin UI vNext — A1 audit followup (APPROVE WITH CHANGES)

Codex A1 verdict on commit `882ebc0`: APPROVE WITH CHANGES
(0 C / 6 H / 10 M / 5 L). All 21 findings folded; revisions
applied to `master.md` + zone-catalog + ownership-matrix +
CODEOWNERS template in this commit.

## H1 — `src/admin-ui/**` not in zone catalog

**Verified:** the original plan routed `src/admin-ui/**` to an
"out-of-catalog `admin-ui` zone", which is a routing-rule
violation (zones must come from the catalog).

**Revision applied:**

1. Added new row `admin-ui` to
   [docs/repository-zones/zone-catalog.md](../../../repository-zones/zone-catalog.md):
   path `src/admin-ui/**`, owner `operator/admin-ui`,
   backup `operator/api`, scope includes React/Vite/MUI/MobX
   stack + routing + theme + stores + API/SignalR clients +
   mocks + e2e; out-of-scope = backend C# / persistence /
   BSV parsing; contracts = typed DTOs parity with C#,
   theme tokens, MobX entrypoints.
2. Added matching row to
   [docs/repository-zones/ownership-matrix.md](../../../repository-zones/ownership-matrix.md)
   with validation evidence `pnpm typecheck + lint + build +
   test + verify` + Playwright smoke + Lighthouse reports.
3. Added `/src/admin-ui/` line to
   [.github/CODEOWNERS.template](../../../../.github/CODEOWNERS.template)
   mapped to `@replace-with-operator-admin-ui`.

## H2 — Auth / RBAC foundation missing

**Verified:** every admin REST endpoint enforces
`AdminAuthDefaults.Policy`; backend ships cookie auth at
`/api/admin/auth/{me,login,logout}`. The original plan had
no auth slice.

**Revision applied:**

- New Core Rule 13: every route except `/login` requires an
  authenticated session.
- S0 scaffold lands a placeholder `/login` route and a 401
  global redirect.
- S2 owns route guards + `/login` + `/setup` + `/logout` UX.
- S3 owns the `me / login / logout` API client + cookie-mode
  integration test + 401/403 normalization into `AppError`
  (Core Rule 8 amended).
- DoD adds "Auth golden path green".

## H3 — S1 theme-gate inconsistency

**Verified:** Product Decision said S0+S1-S3 had slice-level
audit gates, but Core Rule 11 only gated S0/S2/S3.

**Revision applied:** S1 promoted to slice-level audit gate.
Slice ledger now shows S0 + S1 + S2 + S3 as the foundational
quartet, each with `slice-A1`. Product Decision §"Single audit
cadence" rewritten to match (also closes L5).

## H4 — Contract parity not actually verifiable

**Verified:** the backend doesn't publish OpenAPI; the
original plan only promised mock-vs-real shape parity, not
parity with C# DTOs.

**Revision applied:**

- New Core Rule 7: contract source = `src/admin-ui/contracts/swagger.json`
  committed in-repo + a codegen step generates TS types into
  `src/admin-ui/src/types/api.generated.ts`.
- S3 ships the codegen step + a contract-parity test that
  boots the ASP.NET app and validates real endpoint payloads
  against the generated types.
- Core Rule 12 (mock parity) keeps the mock-vs-real shape
  test as a separate concern; both pin against the generated
  types.
- DoD adds "Contract-parity test green against a live ASP.NET
  host".

## H5 — CI doesn't know about the Vite project

**Verified:** `.github/workflows/ci-tests.yml` only runs
`dotnet test`. The Dockerfile builds the admin UI with pnpm,
so CI gaps would only surface in release.

**Revision applied:**

- Core Rule 11 amended: CI is wired in S0.
- S0 validation now requires extending the workflow with
  Node/corepack setup + `cd src/admin-ui && pnpm install
  --frozen-lockfile && pnpm verify` alongside the existing
  `dotnet test`.
- S0 done-when explicitly lists this workflow change.

## H6 — Header globals leak into screen slices

**Verified:** the original plan owned the alert badge in S7
even though AppBar/header shell belongs to S2.

**Revision applied:**

- S2 now owns ALL header globals: AppBar, Drawer, smart
  search, alert badge, connection-status chip, environment
  tag.
- S7 only feeds alert-count + toast state through a store
  contract established in S3.
- S4 + S7 done-when explicitly defer the badge to S2.

## M1 — Alert push UX vs poll reality

**Verified:** backend has no SignalR push for alerts; design
brief's "SignalR everywhere" sentence is stale for alerts.

**Revision applied:**

- S7 done-when: "toasts driven by `/api/admin/p2p/alerts?since=`
  poll-delta with `AlertUnixMs` / `Id` dedupe cursor — NOT
  SignalR push".
- Scope §"Out of scope" explicitly lists SignalR push for
  alerts as a backend follow-up.
- Tests pin toast appearance against a controlled mock
  polling cadence; an explicit assertion confirms no SignalR
  subscriber for alerts exists.

## M2 — Background-cleanup not verified

**Revision applied:**

- Core Rule 6 amended: S3 ships a fake-timer + mocked-
  SignalR harness that proves route mount/unmount cancels
  pollers, removes subscriptions, aborts requests, and
  avoids duplicate listeners after repeated navigation.
- S3 done-when explicitly requires the harness green.

## M3 — Initial JS budget unrealistic

**Verified:** MUI core ~150 KB + DataGrid ~70 KB + X Charts
~50 KB makes the original `< 300 KB` total budget infeasible
without code splitting.

**Revision applied:**

- New budget split:
  - Shell-only initial < 200 KB gzipped.
  - Per-route chunk ≤ 250 KB gzipped (DataGrid- and Charts-
    heavy screens lazy-loaded per-route).
- S9 + S10 done-when require per-route lazy boundaries for
  X Charts and X DataGrid heavy screens.
- S11 enforces the budget assertions in CI.

## M4 — Testing pyramid + file layout

**Revision applied:**

- New Core Rule 15: testing pyramid locked at S0.
  - `unit` = `**/*.test.tsx` (component + store)
  - `integration` = `src/admin-ui/src/integration/` (store +
    API mock + route hydration)
  - `e2e` = `src/admin-ui/tests/e2e/` (Playwright golden
    paths)
  - `contract` = `src/admin-ui/tests/contract/` (backend
    payload/schema parity)
- S0 done-when explicitly pins the file layout + scripts.

## M5 — Mobile coverage residual

**Revision applied:**

- Mobile-handoff gap recorded explicitly (design bundle only
  shipped Dashboard at 375px; screens 6-14 are tokens/
  patterns only).
- S11 done-when adds the mobile QA matrix at 375px + one
  narrow-desktop breakpoint for ALL 14 screens covering
  DataGrid overflow, drawer behavior, dialogs, snackbars.
- DoD adds the mobile-QA gate.

## M6 — Smart-search grammar under-specified

**Revision applied:**

- New Core Rule 14: smart-search grammar decision table
  shipped in S2 with tests pinning every grammar branch +
  the not-found "did you mean" panel.
- S5 done-when: entity routes reached only via the S2
  grammar (not duplicate ad-hoc parsers).

## M7 — Cross-screen invalidation undefined

**Revision applied:**

- Core Rule 4 rewritten: "one MobX store per screen + a root
  event-bus for SignalR fan-out". Root store in
  `src/stores/root.ts` exposes a typed publish/subscribe
  surface; each store declares which hub events it
  subscribes to (`OnNewBlock` / `OnReorg` /
  `OnBroadcastStateChanged`); the bus dispatches with cache
  keys + stale markers.
- S3 ships the bus pattern + tests; S5 and S7 wire it.

## M8 — Configuration tune affordance dropped

**Verified:** original plan kept a feature-flagged tune
affordance "present and off". Risk: design rot if the hidden
UX bit-rots before the backend endpoint lands.

**Revision applied:**

- Configuration screen is READ-ONLY this wave, full stop.
- No feature-flagged tune affordance.
- S10 done-when says explicitly "Configuration is read-only
  — NO feature-flagged tune affordance ships this wave".
- The tune affordance lands when the backend hot-reload
  endpoint ships (recorded as a post-program follow-up).

## M9 — Security hygiene + observability uncovered

**Revision applied:** new Core Rule 9 covers
`frontend-principles.md` §14-15:

- No secrets in client state.
- Raw hex / config keys / auth tokens never logged.
- Raw JSON rendering goes through a sanitizer that elides
  any `*token*` / `*secret*` / `*password*` field.
- Optional structured UI error/event telemetry behind a
  feature flag (no PII / no payloads).

S10 done-when requires the sanitizer running on every
rendered JSON in the Logs/Raw screen.

## M10 — Score gradient contrast at S8, not just S11

**Verified:** the 0-100 score gradient is the only place
contrast might fail WCAG AA at certain stops over the dark
background. S11 is too late if the gradient ships in S8
without verification.

**Revision applied:**

- Core Rule 10 amended: S8 owns the score-gradient contrast
  check in both palettes.
- S8 done-when: gradient contrast report committed.
- S11 re-runs the whole-app audit.

## L1 — Theme persistence migration

**Revision applied:** S1 done-when adds `persistVersion`
field on the mobx-persist-store snapshot + reset behavior
for unknown/old snapshots.

## L2 — Design-token deviations not in Product Decision

**Revision applied:** Product Decision now explicitly accepts
the two design-bundle deviations (`shape.borderRadius: 8` and
`typography.code: JetBrains Mono`) — not only in DoD.

## L3 — Closeout requirements too thin

**Revision applied:** S12 done-when expands closeout doc
requirements per the frontend-audits-playbook §template:

- Scope · sources of truth · validation commands · CI
  evidence · delivery hashes per slice · before/after
  screenshots · contract-parity evidence · mobile/a11y/perf
  reports · accepted design deviations · residuals · open
  assumptions.

## L4 — Atomic S0 deletion + scaffold

**Verified:** Dockerfile assumes `..\admin-ui\package.json` +
runs `pnpm install/build` when present. Partial deletion
without scaffold replacement would silently skip admin UI
publish.

**Revision applied:** S0 done-when requires atomic delete +
scaffold in one commit. Dockerfile admin-ui publish path is
verified at S0 close.

## L5 — Audit cadence contradiction

**Revision applied (also closes H3 in tandem):** Product
Decision now states the single cadence verbatim:

- Slice-level audits for the foundational quartet S0 + S1 +
  S2 + S3.
- Implementation slices S4-S10 covered by the program-level
  A1 + standard per-slice review.
- S11 = whole-app quality audit (a11y + perf).
- S12 = closeout audit (A2).

Core Rule 11 amended to match.

---

## Summary of changes

| Layer | Change |
|---|---|
| `docs/repository-zones/zone-catalog.md` | Added `admin-ui` zone row |
| `docs/repository-zones/ownership-matrix.md` | Added admin-ui task type with validation evidence |
| `.github/CODEOWNERS.template` | Added `/src/admin-ui/` mapping |
| `master.md` §Product Decision | Audit cadence, contract source, atomic S0, deviations |
| `master.md` §Scope | Auth, sanitizer, mobile QA, no-tune-affordance |
| `master.md` §Core Rules | 12 rules → 15 (root event-bus, security hygiene, CI gate, auth gate, search grammar, testing pyramid) |
| `master.md` §Slice Ledger | S1 promoted to slice-A1; S2 owns header globals + search grammar + auth guards; S3 ships event bus + contract parity + cleanup harness; S7 alert toast via poll-delta; S8 contrast check; S10 sanitizer; S11 bundle budgets + mobile matrix; S12 closeout template |
| `master.md` §Definition of Done | Auth golden path, contract parity, bundle budgets split, mobile matrix, gradient contrast, two deviations recorded |

All 21 findings folded. Awaiting Codex re-pass on the revised
plan or proceeding to S0 if the user accepts the revision.
