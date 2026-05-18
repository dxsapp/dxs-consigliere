# Admin UI vNext — S2 slice audit

Target commit: `91f5de4` (`feat(admin-ui): wave-vnext S2 — shell + routing + auth guards + smart search`)
Reviewer: Codex
Date: 2026-05-18

Scope: S2 shell + routing + auth guard + smart-search grammar at `91f5de4`. Current working tree source under `src/admin-ui/**` matches the target commit; the only post-target diff before writing this audit was the S2 audit prompt file.

Covered dimensions:

1. Nav inventory parity
2. Smart-search grammar coverage
3. Ambiguous search UX
4. Auth guard correctness
5. Header globals ownership
6. Mobile responsiveness
7. Route-driven hydration discipline
8. Forward compatibility for S3
9. `/dev/theme-demo` discoverability
10. Active-route highlight
11. Active-route prefix-match bug
12. Smart-search accessibility
13. `HeaderSearch` `onChange` / `onInputChange` split
14. `PlaceholderPage` usefulness
15. `AuthStore` observable semantics
16. Bundle budget headroom
17. Imports and dead code
18. Icon consistency
19. Doc freshness
20. No backend touched

Verification:

- `pnpm verify` from `src/admin-ui`: pass.
- Vitest: 6 files, 36 tests passed.
- Build output: shell chunk `index-DsCiI_Zt.js` is `198.21 kB` gzip; lazy `DevThemeDemoPage` chunk is `4.67 kB` gzip.
- ESLint emitted one warning from pre-existing S1 code: unused `eslint-disable` in `src/stores/pref.store.ts:105`.
- `git show --name-only 91f5de4 -- src/Dxs.Consigliere src/Dxs.Bsv src/Dxs.Common src/Dxs.Infrastructure`: no backend files.
- Playwright package is present, but the local browser binary is not installed, so no screenshot-based mobile proof was produced.

Positive coverage:

- The sidebar inventory matches the design brief's 14 screens: 6 Operator routes and 8 System routes are present in `src/admin-ui/src/app/routes.ts:32`.
- Detail routes for tx, address, and token are authed and wired in `src/admin-ui/src/app/App.tsx:81`, `src/admin-ui/src/app/App.tsx:84`, and `src/admin-ui/src/app/App.tsx:86`.
- Smart-search grammar covers empty input, 64-hex ambiguity, integer block heights, P2PKH/P2SH address shape, base58 non-address token fallback, arbitrary token fallback, and URL encoding (`src/admin-ui/src/lib/search/grammar.ts:35`, `src/admin-ui/src/lib/search/grammar.test.ts:4`).
- `/dev/theme-demo` remains lazy, Suspense-wrapped, outside the auth guard, and absent from the sidebar (`src/admin-ui/src/app/App.tsx:14`, `src/admin-ui/src/app/App.tsx:43`).
- S2 does not add business-data hydration in page effects; the only app-level `useEffect` hydrates UI preferences (`src/admin-ui/src/app/App.tsx:28`).
- `AuthStore` uses MobX `makeAutoObservable`; synthetic sign-in/out mutate observable status, user, and error state cleanly (`src/admin-ui/src/stores/root.ts:44`).

## Findings

### M1 — Drawer route-match policy ignores `NavRoute.prefix`

Severity: MEDIUM
Slice: S2

Issue: `NavRoute.prefix` is declared as the source of truth for detail-route highlighting (`src/admin-ui/src/app/routes.ts:28`), but `NavList` never consumes it (`src/admin-ui/src/components/shell/AppDrawer.tsx:131`). The current active state depends on React Router's default `NavLink` matching behavior, so the route policy in `routes.ts` is dead metadata and there is no test proving `/transactions/:txid`, `/addresses/:address`, or `/tokens/:tokenId` keep the intended parent nav item active.

Recommended fix: Drive `NavLink` matching explicitly from the route model, for example `end={!r.prefix}` or a `useMatch`/`className` function, and add shell tests for a detail route plus at least one non-prefix System route.

### M2 — Header is not responsive enough for 375px

Severity: MEDIUM
Slice: S2

Issue: On `xs`, `AppHeader` keeps the menu button, full smart-search field, connection chip with text, env chip, alert button, theme toggle, density toggle, and logout button in one non-wrapping toolbar (`src/admin-ui/src/components/shell/AppHeader.tsx:58`). The right-side `Stack` remains horizontal with no breakpoint hiding/collapsing (`src/admin-ui/src/components/shell/AppHeader.tsx:70`), while the search field only has `flex: 1` and `maxWidth: 560` (`src/admin-ui/src/components/shell/HeaderSearch.tsx:50`). At a 375px viewport this combination has no credible width budget.

Recommended fix: Add explicit mobile composition: keep menu + compact search + one overflow/action menu visible; hide chip labels or move connection/env/density/logout into a menu at `xs-sm`; set `minWidth: 0` on the search container and toolbar children. Add a 375px shell smoke once Playwright browsers are available.

### M3 — Auth redirect loses query string and hash

Severity: MEDIUM
Slice: S2

Issue: `AuthGuard` stores only `location.pathname` in `state.from` (`src/admin-ui/src/app/AuthGuard.tsx:27`). Smart search routes block lookups through query strings such as `/headers?height=42` and `/headers?hash=...` (`src/admin-ui/src/lib/search/grammar.ts:82`). An unauthenticated operator who lands on one of those URLs will sign in and return to `/headers`, losing the selected block lookup.

Recommended fix: Store `location.pathname + location.search + location.hash` and add a login redirect test for `/headers?height=42`.

### L1 — `/login` renders blank for already-authenticated users

Severity: LOW
Slice: S2

Issue: The comment says an already-authenticated user on `/login` should bounce to the landing path, but the implementation returns `null` (`src/admin-ui/src/screens/login/LoginPage.tsx:40`). This avoids an infinite loop, but it produces a blank screen for a logged-in operator who manually visits `/login`.

Recommended fix: Return `<Navigate to={fromState ?? LANDING_PATH} replace />` when `auth.isAuthenticated`; add a test for the authenticated `/login` case.

### L2 — Shell still mixes two icon systems

Severity: LOW
Slice: S2

Issue: `AppHeader` imports MUI icon components (`src/admin-ui/src/components/shell/AppHeader.tsx:10`), while `AppDrawer` uses Material Symbols string spans (`src/admin-ui/src/components/shell/AppDrawer.tsx:59`, `src/admin-ui/src/components/shell/AppDrawer.tsx:148`). The prompt calls this out as a design-system deviation; keeping both increases visual and bundle inconsistency.

Recommended fix: Pick one shell icon system. If Material Symbols are intentional because the design bundle uses that font, move header icons to the same system or document the exception and centralize icon rendering.

### L3 — README remains stale after S2

Severity: LOW
Slice: S2

Issue: `src/admin-ui/README.md` still says the app status is "S0 scaffold" (`src/admin-ui/README.md:11`) and its layout section says "S0 — fills in over S1-S10" (`src/admin-ui/README.md:44`). It also still describes `screens/login/` as the S0 placeholder even though S2 now ships the login form (`src/admin-ui/README.md:72`).

Recommended fix: Update the README status and tree to reflect S1 tokens, S2 shell/routing/search, current component paths, and remaining S3+ placeholders.

### L4 — Header signal seams are traceable but still hard-coded in `AppShell`

Severity: LOW
Slice: S2

Issue: `AppHeader` correctly accepts `alertCount` and `connection` as props, but `AppShell` passes literal placeholder values directly in JSX (`src/admin-ui/src/components/shell/AppShell.tsx:37`). S3 can replace this, but the current seam is not yet a root-store getter or shell-level selector.

Recommended fix: In S3, feed both values from explicit root-store surfaces, even if initially backed by mocks, so screen slices never need to own header globals.

Verdict: APPROVE WITH CHANGES
Critical findings: 0
High findings: 0
Medium findings: 3
Low findings: 4
Headline: S2 is structurally sound and passes local verification, but S3 should not open until the drawer match policy, mobile header layout, and auth return URL loss are fixed.
