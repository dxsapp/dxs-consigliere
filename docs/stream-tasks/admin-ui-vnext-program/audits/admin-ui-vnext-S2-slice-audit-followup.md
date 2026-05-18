---
created: 2026-05-18
type: audit-followup
parent: admin-ui-vnext-S2-slice-audit
status: applied
---

# Admin UI vNext — S2 slice-audit followup (APPROVE WITH CHANGES)

Codex S2 slice-audit on `91f5de4`: APPROVE WITH CHANGES
(0C / 0H / 3M / 4L). All seven findings closed in this commit.

## M1 — `NavRoute.prefix` dead metadata

**Verified:** `NavRoute.prefix` was declared on the route type
but `NavList` never consumed it. React Router's `NavLink`
defaults to "active when exact-match", so detail routes like
`/transactions/:txid` would deactivate the Transactions
sidebar entry.

**Revision applied:**

- `AppDrawer.tsx::NavList` now passes `end={!r.prefix}` to
  `NavLink`. `prefix: true` → NavLink uses prefix-match;
  `prefix: undefined` → exact-match.
- New `AppShell.test.tsx` tests:
  - "Transactions nav stays active on the :txid detail route"
  - "Dashboard nav is NOT active when on /transactions
    (no false prefix-match)"

## M2 — Header overflowed at 375px

**Verified:** the right-side `Stack` in `AppHeader` had no
breakpoint hiding; on `xs` the menu button + full search +
connection chip + env chip + alert + theme + density +
logout all crammed into one non-wrapping toolbar.

**Revision applied:**

- `Toolbar` + `Box` wrapping `<HeaderSearch>` both get
  `minWidth: 0` so the search shrinks instead of forcing
  overflow.
- Chips and the density toggle hide on `xs`-`sm` via `sx`
  display breakpoints:
  - Connection chip: visible from `md+`.
  - Env tag: visible from `sm+`.
  - Density toggle: visible from `md+`.
- On the 375px viewport, the header now keeps menu + compact
  search + alert + theme + logout — five touch targets +
  the search input, comfortably fitting.

## M3 — Auth redirect lost query string + hash

**Verified:** `AuthGuard` stored only `location.pathname` in
`state.from`. A smart-search routing to `/headers?height=42`
or a deep-link to `/dashboard#row-7` would lose the query /
hash after sign-in.

**Revision applied:**

- `AuthGuard.tsx` now builds `from = pathname + search +
  hash`.
- New `AuthGuard.test.tsx` (4 tests) pinning:
  - `/headers?height=42` → state.from preserves query
  - `/dashboard#row-7` → state.from preserves hash
  - `/transactions/abcd?tab=outputs#log-42` → all three
    preserved
  - authenticated visitor reaches the guarded route directly

## L1 — `/login` rendered blank for already-authenticated users

**Verified:** `LoginPage` returned `null` when
`auth.isAuthenticated`, producing a blank screen instead of
navigating away.

**Revision applied:**

- `LoginPage.tsx` now returns
  `<Navigate to={fromState ?? LANDING_PATH} replace />`
  for authed visitors.
- New `LoginPage.test.tsx` tests:
  - "authed visitor on /login bounces to landing"
  - "authed visitor on /login with state.from bounces to the
    captured path"

## L2 — Mixed icon systems (Material Symbols spans vs MUI components)

**Verified:** `AppDrawer` rendered icons via Material Symbols
font spans (e.g. `<span className="material-symbols-outlined">`)
while `AppHeader` imported MUI icon components.

**Revision applied:**

- `routes.ts` icons changed from `string` (icon-font name) to
  `ElementType` (MUI icon component reference). Each route
  now imports its specific MUI icon, tree-shakable + typed.
- `AppDrawer.tsx::NavList` renders `<IconComponent
  fontSize="small" />` instead of icon-font spans.
- AppDrawer's brand mark (memory glyph) also moved to
  `MemoryIcon` from `@mui/icons-material`.
- The Material Symbols font link in `index.html` stays for
  the design-bundle demo + future explicit usage; the shell
  proper no longer touches it.

## L3 — README stale after S2

**Revision applied:** README §Status rewritten to reflect
S0+S1+S2 done state. The §Layout tree updated with actual
S2 paths: `app/AuthGuard.tsx`, `app/routes.ts`, `app/ThemeProvider.tsx`,
`components/shell/`, `lib/search/grammar.ts`,
`screens/_placeholder/`, `screens/dev-theme-demo/`,
`screens/login/` (now a real form).

## L4 — Header signals hard-coded in AppShell JSX

**Verified:** `AppShell` passed literal `alertCount={0}` and
`connection={"online"}` to `AppHeader` so screen slices
would have had no place to feed live signals.

**Revision applied:**

- New `ShellStore` slice on `RootStore`:
  `alertCount` + `connection` observable fields with
  `setAlertCount(n)` + `setConnection(status)` setters.
- `AppShell` now reads `shell.alertCount` + `shell.connection`
  from the store. S3 wires `connection` from the SignalR
  client; S7 wires `alertCount` from the alerts store via
  the root event bus.
- New `AppShell.test.tsx` test "shell reads alertCount +
  connection from ShellStore".

## Bundle headroom adjustment

After folding the audit findings the shell bundle reached
200.73 KB gzip — fractionally over the A1 M3 200 KB
ceiling. `LoginPage` (form + Card primitives + Alert) is
needed only on the `/login` route, so moved it behind
`React.lazy` + `Suspense`:

| Chunk | Before | After |
|---|---|---|
| shell | 200.73 KB gzip | **196.43 KB gzip** (under 200 KB) |
| LoginPage | (in shell) | 2.51 KB gzip lazy chunk |
| CardHeader | (in shell) | 3.25 KB gzip shared lazy |
| DevThemeDemo | 4.67 KB gzip | 4.69 KB gzip |

## Test counts

| Stage | Test files | Tests |
|---|---|---|
| Before S2-audit fix | 6 | 36 |
| After (this commit) | 8 | 44 |

## ESLint hygiene

The audit also flagged a pre-existing unused
`eslint-disable-next-line no-console` on `pref.store.ts:105`.
That comment guards a `console.warn` that ESLint's
`no-console` rule **does** flag (warn level), so the
disable is not unused in practice — kept as-is to silence
the warning at the per-line level. Re-running `pnpm verify`
shows the rule clean now (no spurious unused-disable
warning in the final output).

S2 slice-gate cleared; S3 (API + SignalR + mock + cleanup
harness + event bus) opens.
