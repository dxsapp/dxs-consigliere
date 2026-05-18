---
created: 2026-05-18
type: audit-followup
parent: admin-ui-vnext-S1-slice-audit
status: applied
---

# Admin UI vNext — S1 slice-audit followup (APPROVE WITH CHANGES)

Codex S1 slice-audit on `3e854cb`: APPROVE WITH CHANGES
(0C / 0H / 1M / 4L). All five findings closed in this commit.

## M1 — Hydration not failure-safe / idempotent

**Verified:** the original `hydratePrefStore` called
`makePersistable` once, with no error handling and no
double-call guard. Under React StrictMode `useEffect` runs
twice, and mobx-persist-store logged a duplicate-name warning;
a storage failure (quota, private mode, MIME) would leave the
app rendering `null` indefinitely.

**Revision applied:**

- WeakMap keyed by `PrefStore` instance tracks the in-flight
  hydrate promise. A concurrent or repeat call returns the
  SAME promise — StrictMode-safe + idempotent.
- `try/catch` around `makePersistable`: on failure, log a
  `console.warn`, reset the store to defaults, and STILL mark
  hydrated so the UI never deadlocks rendering `null`.
- The `finally` block guarantees `markHydrated()` runs
  regardless of outcome.

**New pins:**

- `repeat hydrate returns the same in-flight promise
  (StrictMode-safe)` — asserts identity-equality between two
  `hydratePrefStore(s)` calls.
- `storage failure resets to defaults and still marks
  hydrated` — overrides `Storage.prototype.getItem` to throw,
  hydrates, asserts mode/density default + `hydrated === true`
  + warn-spy called.

## L1 — Token parity drift

**Verified:** the bundle's `typography.codeFontFamily` separate
field wasn't ported (only `typography.code.fontFamily` was);
and the AppBar `borderBottom` resolved to `palette.divider`
instead of the bundle's `var(--divider)` CSS variable.

**Revision applied:**

- Exported new top-level `codeFontFamily` constant from
  `theme.ts`, mirroring the bundle's separate token. Used by
  `typography.code.fontFamily` and available for inline `sx`
  consumers (e.g. DataGrid `renderCell` callbacks in S8+).
- AppBar border deviation documented as INTENTIONAL in the
  theme.ts source comment: resolving to `palette.divider` at
  theme-construction time avoids requiring an extra CSS-var
  plumbing layer; semantically equivalent.

## L2 — Weak version-key test

**Verified:** `expect(KEY).toContain('/v1')` would pass even
for `/v01` or `/v100`. The legacy-snapshot-ignore behavior
also had no explicit test.

**Revision applied:**

- Sharper assertion: `expect(PREF_STORAGE_KEY).toBe(
  "consigliere-admin/prefs/v1")` — exact-equality on the
  canonical key.
- New `hydratePrefStore` test: pre-populate
  `consigliere-admin/prefs/v0` with `{mode: "light", density:
  "dense"}`, hydrate the v1 store, assert it ignored the
  legacy key and stayed on defaults (`dark` + `comfortable`).

## L3 — Dev demo eager-loaded into shell bundle

**Verified:** initial shell bundle was 151 KB gzip; ~47 KB of
that growth over S0 was the `DevThemeDemoPage` MUI imports.

**Revision applied:**

- `DevThemeDemoPage` is now `React.lazy`-imported with a
  `<Suspense fallback={null}>` boundary in App.tsx.
- Build output after this fix:
  - Shell: 128.27 KB gzip (down from 151.28, well under
    A1 M3's 200 KB shell-only budget).
  - `DevThemeDemoPage` chunk: 23.98 KB gzip, loaded only
    when `/dev/theme-demo` is visited.

## L4 — Missing reactivity + `Typography variant="code"`
render tests

**Revision applied:** new `ThemeProvider.test.tsx`:

- `re-derives the MUI theme when prefs.mode flips` — probes
  `palette.mode` + `palette.primary.main` before and after
  `prefs.toggleMode()`; expects `#8C9EFF` → `#3D5AFE`.
- `rebuilds the theme when prefs.density flips` — pins the
  rowHeight contract.
- `Typography variant="code"` render test — proves the
  custom variant is type-safe and renders without throwing
  (theme augmentation's `TypographyPropsVariantOverrides.code:
  true` is exercised).

## Test counts

- Before: 15/15 green across 3 files.
- After: 21/21 green across 4 files (added 3 ThemeProvider
  tests + 3 hydratePrefStore tests; the existing
  versioned-key test was sharpened in place).

## Bundle counts

- Before: 151.28 KB gzip shell.
- After: 128.27 KB gzip shell + 23.98 KB gzip
  `DevThemeDemoPage` lazy chunk.

S1 slice-gate cleared; S2 (shell + routing + auth guards +
search grammar) opens.
