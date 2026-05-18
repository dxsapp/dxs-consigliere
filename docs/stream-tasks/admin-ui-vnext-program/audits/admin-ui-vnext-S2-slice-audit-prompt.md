# Admin UI vNext — S2 slice-audit prompt

Audit target: S2 (shell + routing + auth guards + smart-search
grammar) at commit `91f5de4` on `codex/consigliere-vnext`. Run
sync; this gates S3+ open.

---

You are auditing the **third foundational slice** (S0 + S1 +
S2 are gated). S2 lands the AppShell + Drawer + Header + auth
guard + smart-search grammar + LoginPage form + placeholder
pages for all 14 screens. S3 will replace synthetic auth with
real cookie-mode calls; S4-S10 swap placeholders for real
screens.

Read:

- `docs/stream-tasks/admin-ui-vnext-program/master.md` (the
  approved plan; S2 row marked done)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-S1-slice-audit-followup.md`
  (S1 baseline going into S2)
- `docs/admin-ui/design-handoff/00-design-brief.md` §4 IA
  (the canonical sidebar inventory) + §6.2 smart-search
  contract
- `/Users/imighty/Code/docs/frontend-principles.md` §16-17
  route-driven hydration, §9 design-system primitives, §11
  responsive/a11y defaults

Cross-validate against the S2 deliverable at `91f5de4`:

- `src/admin-ui/src/lib/search/grammar.ts` + `grammar.test.ts`
- `src/admin-ui/src/app/routes.ts`
- `src/admin-ui/src/app/AuthGuard.tsx`
- `src/admin-ui/src/app/App.tsx`
- `src/admin-ui/src/components/shell/AppShell.tsx`
- `src/admin-ui/src/components/shell/AppHeader.tsx`
- `src/admin-ui/src/components/shell/AppDrawer.tsx`
- `src/admin-ui/src/components/shell/HeaderSearch.tsx`
- `src/admin-ui/src/components/shell/AppShell.test.tsx`
- `src/admin-ui/src/screens/login/LoginPage.tsx` +
  `LoginPage.test.tsx`
- `src/admin-ui/src/screens/_placeholder/PlaceholderPage.tsx`
- `src/admin-ui/src/stores/root.ts` (AuthStore extension)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Nav inventory parity (design brief §4).** Diff
   `routes.ts` against the 14-screen IA the brief specifies.
   - 6 Operator: Dashboard, Transactions, Broadcast Queue,
     Addresses, Tokens, Alerts.
   - 8 System: P2P Pool, Source Metrics, Headers Chain,
     Broadcast Inspector, Configuration, Logs/Raw, Providers,
     Setup.
   - Detail routes (`:txid` / `:address` / `:tokenId`) are
     NOT in the sidebar but ARE wired in App.tsx as authed
     routes.
   Flag any drift in label / path / section.

2. **Smart-search grammar coverage (A1 M6 / Core Rule §14).**
   Verify the decision table covers every brief-specified
   branch:
   - empty / whitespace → empty
   - 64-hex → ambiguous(tx-first, block-hash alt)
   - positive integer → block-height
   - base58 address-shape (P2PKH 1… / P2SH 3…) → address
   - everything else (incl. base58 non-addr-shape) → token
   - URL-encoding for path segments with special chars
   Flag missing branches or wrong ordering (e.g. integer
   matching before hex would change behaviour for "1234").

3. **`Ambiguous` UX delivery.** `HeaderSearch` opens the
   Autocomplete dropdown when grammar returns `ambiguous`.
   Verify:
   - Operator can click a candidate and the router actually
     navigates.
   - The "did you mean ..." copy is visible.
   - There's no accidental Enter-double-fire after picking.
   - State is reset when the user starts typing again.

4. **Auth guard correctness (A1 H2 + Core Rule §13).**
   `AuthGuard` redirects unauthenticated visitors to
   `/login` with `state.from` set. Verify:
   - The guard wraps EVERY route except `/login` and
     `/dev/theme-demo`.
   - The state-encoded `from` is correctly used by the login
     form (it must navigate back to that path on success).
   - There's no infinite redirect loop if the user clicks
     "Sign in" while already authenticated.
   - The 401 redirect from the API client
     (`lib/api/client.ts`) and this client-side guard
     compose cleanly (one handles server-driven expiry, the
     other client-only navigation).

5. **Header globals ownership (A1 H6).** Every header global
   from the design brief is owned by `AppHeader`, not by a
   screen slice:
   - Smart-search bar
   - Alert badge (count is a prop today; the source contract
     between AppHeader and a future alert store should be
     traceable in code).
   - Connection-status chip
   - Env tag
   - Theme mode toggle
   - Density toggle
   - Logout button
   Flag anything a screen slice would have to override later.

6. **Mobile responsiveness.** `AppDrawer` switches from
   `permanent` (md+) to `temporary` (xs-sm) via
   `useMediaQuery`. Verify:
   - The temporary Drawer can be closed by clicking outside.
   - The Header's menu button appears ONLY on mobile.
   - The shell stacks correctly on the 375px viewport (the
     S2 brief says mobile is in scope but only Dashboard has
     a hi-fi mock; verify no obvious overflow / hidden CTA).

7. **Route-driven hydration discipline (Core Rule §3 /
   frontend-principles §16-17).** Verify no S2 component
   owns business data hydration via `useEffect`. The hydrate
   call in `App.tsx` is for prefs (UI state), not business
   data — that's acceptable.

8. **Forward-compat for S3 (API client + event bus).** The
   S3 plan adds:
   - SignalR client + reconnect + per-widget stale callback
   - Event bus in root store
   - Cookie-mode auth client replacing the synthetic methods
   Does S2 leave the right seams? Specifically:
   - `AuthStore` exposes a clean method-call surface that
     S3 can swap for real fetches.
   - `AppHeader`'s `connection` prop is set to `"online"`
     today; S3 needs to feed it from SignalR. Verify it's a
     simple prop or store getter, not a hard-coded constant
     in JSX.
   - `alertCount` likewise.

9. **`/dev/theme-demo` discoverability.** S1's lazy demo
   route survives. Verify:
   - It's still lazy + Suspense-wrapped.
   - It's OUTSIDE the auth guard (a deliberate choice or an
     oversight?).
   - It's NOT in the sidebar.

10. **Active-route highlight in the drawer.** `NavLink` adds
    `.active` class; the `sx` in `AppDrawer` styles it.
    Verify:
    - Detail routes (`/transactions/:txid`) trigger the
      Transactions nav highlight — does the `prefix: true`
      flag on the NavRoute actually drive a `NavLink end`
      override? (Currently the `prefix` flag is read on the
      type but I don't see it consumed.)

11. **Active route prefix-match bug.** `NavRoute.prefix` is
    declared but the `NavList` doesn't consume it. NavLink
    defaults to "active when exact-match"; `:txid` detail
    routes would deactivate Transactions in the sidebar.
    Flag as a finding (likely M).

12. **Smart-search input accessibility.** `HeaderSearch`
    uses MUI Autocomplete which gives keyboard nav for free.
    Verify:
    - The TextField has a meaningful `aria-label` or
      `placeholder` (placeholder is present).
    - Pressing Enter inside the input doesn't accidentally
      submit a wrapping form (there's no form, but worth
      pinning).
    - The dropdown is keyboard-navigable + screen-reader-
      friendly.

13. **`HeaderSearch.tsx` — onChange-vs-onInputChange split.**
    The component uses both `inputValue/onInputChange` and
    `onChange`. Verify the contract: `onChange` only fires
    when the user picks from the dropdown (i.e. the
    ambiguous candidates); `onInputChange` mirrors live
    typing. Edge case: pasting a 64-hex string + pressing
    Enter — does ambiguity panel open first, or does Enter
    pre-empt?

14. **PlaceholderPage usefulness.** It shows id, owner
    slice, route params. Verify it's typed correctly (no
    `any`) and that the `:txid` / `:address` / `:tokenId`
    route params actually surface. Smoke-render to confirm.

15. **AuthStore observable semantics.** `signInSynthetic` /
    `signOutSynthetic` mutate observables; `isAuthenticated`
    is a getter. Verify:
    - The getter is marked computed (`makeAutoObservable`
      handles this) so it doesn't re-evaluate unnecessarily.
    - Logging out actually clears `user` + `lastError`.
    - There's no race between AuthGuard reading
      `isAuthenticated` and the synthetic flip (mobx-react-
      lite's `observer` should re-render synchronously, but
      pin it).

16. **Bundle budget headroom.** Shell 198 KB gzip is just
    UNDER the A1 M3 200 KB ceiling. As S3-S10 land, where
    does new weight go? Are S3's API/SignalR/mock + S4
    Dashboard expected to fit in the shell, or do they need
    route-level lazy boundaries to avoid breaching 200 KB?

17. **Imports + dead code.** Spot-check for unused imports
    (TS strict + ESLint should catch most). The Material
    Symbols `<span>` icon usage in AppDrawer is intentional
    (matches the design bundle's icon-font approach) — is
    that consistent with the rest of the codebase, or does
    `@mui/icons-material` import everywhere else?

18. **`MenuIcon` consistency with the icon-font.**
    `AppHeader` imports MUI icon components
    (`MenuIcon` etc.) while `AppDrawer` uses Material
    Symbols via `<span className="material-symbols-
    outlined">`. Two icon systems in the same shell. Flag
    as a deviation (likely L).

19. **Doc freshness.** S2 didn't update master.md's S2-row
    status (it now reads "done"). Verify the README + any
    other doc that names a specific path is current. Also
    confirm `master.md` table-style lint was left alone (we
    intentionally ignore that).

20. **No backend touched.** Diff
    `src/Dxs.Consigliere/**` for the S2 commit — should be
    empty.

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
- Slice (S2 or program-level)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
