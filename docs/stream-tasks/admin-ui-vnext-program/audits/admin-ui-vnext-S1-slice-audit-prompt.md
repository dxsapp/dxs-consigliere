# Admin UI vNext — S1 slice-audit prompt

Audit target: S1 (design tokens + MUI theme + pref
persistence) at commit `3e854cb` on
`codex/consigliere-vnext`. Run sync; this gates S2+ open.

---

You are auditing the **second** foundational slice of the
admin-ui-vnext program. S1 ports the design bundle's tokens
into a working MUI theme and adds the MobX pref store that
drives mode + density with versioned persistence. S2 (shell
+ routing + auth guards + search grammar) depends on the
theme being faithful and the persistence being robust.

Read:

- `docs/stream-tasks/admin-ui-vnext-program/master.md`
  (the approved plan; S1 row done)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S0-slice-audit-followup.md`
  (S0 baseline before S1 lands)
- `docs/admin-ui/design-bundle/project/theme.jsx` (THE source
  of truth for the tokens; S1 is a 1:1 port)
- `docs/admin-ui/design-handoff/00-design-brief.md` §8
  (MUI mapping table the theme must honour)
- `/Users/imighty/Code/docs/project-stack-profiles.md`
  (workspace baseline — `framer-motion`, MobX, MUI 7)
- `/Users/imighty/Code/docs/frontend-principles.md`
  (esp. §9 design-system primitives, §16-17 route-driven
  hydration, §22 doc freshness)

Cross-validate against the S1 deliverable at `3e854cb`:

- `src/admin-ui/src/app/theme.ts`
- `src/admin-ui/src/app/theme-augmentation.ts`
- `src/admin-ui/src/app/ThemeProvider.tsx`
- `src/admin-ui/src/app/App.tsx` (S1 changes the bootstrap)
- `src/admin-ui/src/stores/pref.store.ts`
- `src/admin-ui/src/stores/root.ts` (S1 adds the prefs slice)
- `src/admin-ui/src/screens/dev-theme-demo/DevThemeDemoPage.tsx`
- `src/admin-ui/src/app/theme.test.ts`
- `src/admin-ui/src/stores/pref.store.test.ts`

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Token parity with the bundle.** Diff
   `src/admin-ui/src/app/theme.ts` against
   `docs/admin-ui/design-bundle/project/theme.jsx` field by
   field. Specifically:
   - Both palettes (primary / secondary / severity / score /
     background / text / divider / action) match exactly.
   - Typography scale matches (h1-h6, subtitle1/2, body1/2,
     caption, overline, code).
   - `shape.borderRadius: 8`, `spacing: 8`.
   - `rowHeight {comfortable:52, dense:36}`.
   - `layout.appBarHeight {desktop:64, mobile:56}`,
     `layout.drawerWidth: 240`.
   - `components.MuiAppBar / MuiCard / MuiChip / MuiButton`
     defaultProps + AppBar styleOverrides (backdropFilter
     blur 8px + bottom divider).
   - `MuiDataGrid.defaultProps.density` flips on density
     argument.
   Flag any drift, even cosmetic.

2. **MUI module augmentation completeness.** Verify
   `theme-augmentation.ts` covers every custom field the
   feature code will read:
   - `Palette.severity` + `PaletteOptions.severity` with the
     four SeverityPaletteColor entries.
   - `Palette.score` + `PaletteOptions.score` with the 5
     stops.
   - `TypeBackground.elev1` + `elev2`.
   - `TypographyVariants.code` +
     `TypographyVariantsOptions.code`.
   - `Theme.rowHeight` + `ThemeOptions.rowHeight`.
   - `Theme.layout` + `ThemeOptions.layout`.
   - `MuiDataGrid` theme augmentation imported for-side-
     effects.
   - `TypographyPropsVariantOverrides.code: true` so
     `<Typography variant="code">` is type-safe.
   Flag any missing piece or any place where `any` / type
   loosening would let invalid input through.

3. **Reactive theme rebuild.** `ThemeProvider` is an
   `observer` and re-derives via `useMemo([mode, density])`.
   Verify:
   - The component is wrapped in `observer` (mobx-react-lite),
     not a `useObserver` call.
   - `useMemo` dependency list is exactly `[mode, density]`
     — neither over-narrow (recomputes too rarely → stale
     theme) nor over-wide (recomputes every render → wasted
     work).
   - `CssBaseline` is INSIDE the `MuiThemeProvider` (CSS
     resets need the resolved theme).

4. **Persistence migration robustness (A1 L1).** Verify the
   `persistVersion` approach actually rejects legacy
   snapshots:
   - `PREF_STORAGE_KEY` includes the version, so v1 and v2
     keys are physically distinct in localStorage.
   - `PREF_PERSIST_VERSION` is a single source of truth that
     bumps when tokens are renamed.
   - On hydration, an old snapshot under the OLD key has no
     effect on the v1 store (the v1 key won't exist for
     legacy users; they default fresh).
   - Test pinning: there's a unit test asserting the
     versioned key. Verify it's NOT vacuous (e.g.
     `expect(KEY).toContain('v1')` would pass even with
     `'v100'`; a sharper assertion is better).

5. **`mobx-persist-store` wiring correctness.** Inspect
   `hydratePrefStore` for:
   - `makePersistable` is awaited so callers know when
     hydration completed.
   - `properties: ["mode", "density"]` only — `hydrated`
     itself must NOT be persisted (it's transient).
   - Storage adapter is real `window.localStorage` in
     browser; a no-op in tests/SSR.
   - No double-hydration risk on hot-reload.

6. **No flash-of-default-theme.** `App.tsx` renders `null`
   until prefs are hydrated. Verify:
   - This is the ONLY place that gates on `hydrated` (no
     downstream component needs its own gate).
   - The `useEffect` runs once (empty dependency array).
   - `void hydratePrefStore(...)` correctly discards the
     promise; no unhandled rejection on storage error.

7. **System-pref fallback.** `systemPreferredMode()` reads
   `window.matchMedia("(prefers-color-scheme: light)")` —
   note the inversion (matches=true → light, default →
   dark). Verify:
   - The logic is correct (not flipped).
   - There's no live listener for OS-pref CHANGES (i.e.
     once the user has a persisted choice, OS toggling no
     longer flips the app — is this the intended product
     decision? Flag if undocumented).

8. **`/dev/theme-demo` discoverability.** The route is
   reachable from the public AuthedPlaceholder text. Verify:
   - The route is NOT in the production sidebar plan (S2
     should not add it as a sidebar item).
   - It's behind a `/dev/` prefix conventionally → flag if
     S2 sidebar accidentally pulls it in later.

9. **Test discipline.** Unit tests in
   `theme.test.ts` + `pref.store.test.ts` cover most of the
   factory + store. Flag missing pins:
   - Light palette specific values (currently dark gets the
     bulk of asserts; light is mostly covered too — verify).
   - The Typography `code` variant is registered with the
     `code: true` propVariantOverride (not just the style
     fields) — would a render-test of `<Typography
     variant="code">` catch a type regression?
   - `ThemeProvider` reactivity (no test currently
     exercising "change prefs.mode → theme re-derives").
   - The `DevThemeDemoPage` has no test; is one warranted?

10. **A11y posture of the demo page.** The
    `DevThemeDemoPage` toggles use `IconButton` with
    `aria-label`. Verify:
    - All icon-only buttons have labels.
    - The page renders meaningfully when `prefers-reduced-
      motion` is set (any sneaking animation that bypasses
      framer-motion preferences).
    - Score gradient uses `getContrastText` for legibility —
      verify the result is actually readable across all 5
      stops (this is the A1 M10 contrast concern showing
      up early on the dev page).

11. **Security hygiene (Core Rule §9).** The demo page
    renders a long hex string. Verify it's plain markup
    (not pulled from any token / secret source), and that
    no `console.log` slipped in.

12. **Bundle size impact.** Build output grew from 104 KB
    → 151 KB gzipped. Diff the chunks; verify the growth is
    attributable to the demo page (`screens/dev-theme-demo/`
    + MUI primitives it imports). S2 plan calls for
    route-level lazy boundaries for X DataGrid/X Charts —
    flag if the demo page should ALSO be lazy-loaded so it
    doesn't fatten the production shell.

13. **Forward-compatibility for S2.** S2 will replace
    `AuthedPlaceholder` with the AppBar + Drawer shell. Do
    the S1 placeholders cleanly NOT block that move? In
    particular:
    - Does `App.tsx` still wire the router around the
      ThemeProvider, or does S2 need to refactor?
    - Does the prefs store have toggleMode/toggleDensity at
      a place S2 header icons can call?
    - Are theme tokens accessible from feature stores via
      `useTheme()` only, or did anything global leak?

14. **TS strictness held.** S1 added new types; verify
    `strict`, `noUnusedLocals`, `noUnusedParameters` are
    still honoured. Spot-check unused imports / variables.

15. **README / master.md freshness.** Does the README's S0
    note get updated for S1? Does the master.md S1 row
    reflect actual files + the slice-A1 audit? Spot-check.

16. **No backend touched.** S1 should not modify any C#
    source, Raven model, or admin REST endpoint. Verify the
    diff for `src/Dxs.Consigliere/` is empty in this slice.

17. **Lockfile drift.** `pnpm-lock.yaml` should be
    unchanged unless a new dep was added. Verify the
    `package.json` deps diff (if any) is necessary +
    minimal.

18. **`buildTheme` purity.** Called with identical args, it
    should produce a deep-equal theme each time (no
    accidental Date.now / Math.random / closure state).
    Verify by inspection.

19. **MUI 7 breaking-change exposure.** MUI 7 had a few
    breaking changes (e.g. some default elevations changed,
    `experimentalStyledComponents` flag, etc). Does S1's
    theme assume any MUI 6 default that flipped in 7?

20. **`DevThemeDemoPage` density preview accuracy.** The
    page shows three rows at `theme.rowHeight[density]`
    height. Verify the value flips when the user toggles
    density (i.e. `observer` is doing its job here).

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
- Slice (S1 or program-level)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
