# Admin UI vNext — S1 Slice Audit

Target commit: `3e854cb` (`feat(admin-ui): wave-vnext S1 — design tokens + MUI theme + pref persistence`)
Reviewer: GPT-5 Codex
Date: 2026-05-18

## Scope

Audited S1 against:

- `docs/stream-tasks/admin-ui-vnext-program/master.md`
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S0-slice-audit-followup.md`
- `docs/admin-ui/design-bundle/project/theme.jsx`
- `docs/admin-ui/design-handoff/00-design-brief.md` §8
- `/Users/imighty/Code/docs/project-stack-profiles.md`
- `/Users/imighty/Code/docs/frontend-principles.md`
- Actual S1 deliverables under `src/admin-ui/src/app/`, `src/admin-ui/src/stores/`, and `src/admin-ui/src/screens/dev-theme-demo/`

## Validation

- `pnpm verify` from `src/admin-ui/`
  - `typecheck`: passed
  - `lint`: passed
  - `test`: 3 files / 15 tests passed
  - `build`: passed, `151.28 KB` gzip main JS
- `git diff --name-only 3e854cb^ 3e854cb -- src/Dxs.Consigliere`: no backend files touched.
- `git diff --name-status 3e854cb^ 3e854cb -- src/admin-ui/package.json src/admin-ui/pnpm-lock.yaml`: no package or lockfile changes in S1.

## Checks That Passed

- Palette, severity, score, background, text, divider, action, shape, spacing, row-height, layout, and density-switching tokens are materially ported from `theme.jsx` into `theme.ts`.
- `theme-augmentation.ts` covers custom palette fields, `TypeBackground.elev1/elev2`, `Typography` `code`, `Theme.rowHeight`, `Theme.layout`, MUI DataGrid theme augmentation, and `TypographyPropsVariantOverrides.code`.
- `ThemeProvider` is wrapped in `observer`, derives theme via `useMemo([prefs.mode, prefs.density])`, and renders `CssBaseline` inside the resolved MUI provider.
- `/dev/theme-demo` is reachable from the placeholder text and remains a `/dev/` route, not a production sidebar concept.
- Demo icon-only buttons have `aria-label`; the demo uses plain static hex markup and no `console.log`.
- S2 forward path is clean: the router already wraps content in the reactive theme provider, and `PrefStore` exposes `toggleMode` / `toggleDensity` for future header controls.
- TypeScript strictness held; no unused imports/variables surfaced under `pnpm verify`.
- S0 audit followup items are present before S1: root `Dockerfile` is tracked, README pnpm/CI drift is fixed, and `master.md` uses the top-level `integration/` path.

## Findings

### M1

- Severity: MEDIUM
- Slice: S1
- Issue: Preference hydration is not failure-safe or idempotent. `App.tsx` calls `void hydratePrefStore(root.prefs).then(() => setHydrated(true))` with no `.catch(...)` (`src/admin-ui/src/app/App.tsx:28-30`), so a storage/mobx-persist failure can leave the app rendering `null` indefinitely and still emit an unhandled rejection. Because `main.tsx` uses React `StrictMode`, the mount effect can also run twice against the same module-level `root` store, while `hydratePrefStore` has no "already hydrated/in-flight" guard (`src/admin-ui/src/stores/pref.store.ts:77-85`).
- Recommended fix: Make `hydratePrefStore` idempotent per store, catch storage/persistence failures by resetting defaults and marking the store hydrated, and add tests covering rejected storage and StrictMode-style double invocation.

### L1

- Severity: LOW
- Slice: S1
- Issue: The port is not exact 1:1 token parity in two places. The bundle includes `typography.codeFontFamily` (`docs/admin-ui/design-bundle/project/theme.jsx:47-65`), but `theme.ts` only ports `typography.code.fontFamily` (`src/admin-ui/src/app/theme.ts:23-50`); the bundle's `MuiAppBar` border uses `1px solid var(--divider)` (`theme.jsx:77-80`), while `theme.ts` substitutes the resolved palette divider color (`src/admin-ui/src/app/theme.ts:122-128`). The resolved border is likely better for runtime, but the S1 prompt explicitly says to flag cosmetic drift.
- Recommended fix: Either port `codeFontFamily` and the exact AppBar border token, including augmentation if needed, or document these as intentional implementation deviations in `theme.ts` and the S1 closeout/followup.

### L2

- Severity: LOW
- Slice: S1
- Issue: The persistence version test is weak and does not prove legacy snapshots are ignored. It only checks `PREF_STORAGE_KEY` contains `/v${PREF_PERSIST_VERSION}` and that the version is `1` (`src/admin-ui/src/stores/pref.store.test.ts:41-44`), which would not catch several malformed key patterns or old-key hydration behavior.
- Recommended fix: Assert the exact key value (`consigliere-admin/prefs/v1`) and add a hydration test with an old key populated and the current key absent, proving the store hydrates to defaults.

### L3

- Severity: LOW
- Slice: S1
- Issue: The dev theme demo is eagerly imported into the production initial bundle (`src/admin-ui/src/app/App.tsx:4`, `src/admin-ui/src/app/App.tsx:41`). The bundle is still below the shell budget at `151.28 KB` gzip, but S1 added roughly 47 KB gzip over S0 and the demo is a dev-only visual baseline, so it should not permanently fatten the production shell.
- Recommended fix: Lazy-load `/dev/theme-demo` with `React.lazy`/`Suspense` before S2 closes, or remove the route from production builds behind a dev flag. Keep the theme itself in the shell; move the demo-only MUI primitives/icons to a separate route chunk.

### L4

- Severity: LOW
- Slice: S1
- Issue: ThemeProvider and demo reactivity are only verified indirectly. There is no test that toggling `prefs.mode` or `prefs.density` re-derives the MUI theme, and no render test for `<Typography variant="code">` despite the augmentation being a key S1 deliverable (`src/admin-ui/src/app/theme-augmentation.ts:74-78`, `src/admin-ui/src/screens/dev-theme-demo/DevThemeDemoPage.tsx:86-88`).
- Recommended fix: Add a small component test that renders under `ThemeProvider`, toggles mode/density, asserts palette/row-height updates, and renders `Typography variant="code"` as a type/runtime smoke.

## Residual Risk

S1 is good enough to unblock S2, provided the persistence hardening is handled before or during S2. The theme tokens are usable, the demo route validates the main primitives, and quality gates pass. The main operational risk is a blank app if preference hydration rejects or double-initializes in development.

Verdict: APPROVE WITH CHANGES
Critical findings: 0
High findings: 0
Medium findings: 1
Low findings: 4
Headline: S1 delivers the MUI theme and preference store, but preference hydration needs failure/idempotency hardening before the shell depends on it.
