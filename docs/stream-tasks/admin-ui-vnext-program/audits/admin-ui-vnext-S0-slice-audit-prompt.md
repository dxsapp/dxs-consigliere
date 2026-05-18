# Admin UI vNext — S0 slice-audit prompt

Audit target: S0 (foundational scaffold slice) at commit
`379b7c4` on `codex/consigliere-vnext`. Run sync; this gates
S1+ open.

---

You are auditing the **first slice** of the admin-ui-vnext
program. S0 is the project scaffold + CI + auth-aware
skeleton — every subsequent slice (S1 theme, S2 shell, S3
API/SignalR/mock, S4-S10 screens, S11 polish, S12 closeout)
depends on it.

Read:

- `docs/stream-tasks/admin-ui-vnext-program/master.md`
  (the post-A1-pass-2 plan; status: approved)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-audit-A1-followup.md`
  (all 21 pass-1 findings folded — particularly H2 auth, H4
  contract source, H5 CI, M4 testing pyramid, L4 atomic S0)
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-audit-A1-pass2.md`
  (pass-2 APPROVE)
- `/Users/imighty/Code/docs/project-stack-profiles.md` (the
  workspace stack baseline — hard constraint)
- `/Users/imighty/Code/docs/frontend-principles.md` (the 22
  universal principles)
- `/Users/imighty/Code/docs/frontend-audits-playbook.md`
  (audit cadence + criteria)

Cross-validate against the actual S0 deliverable (commit
`379b7c4`):

- `src/admin-ui/package.json` — deps + scripts + pnpm version
- `src/admin-ui/pnpm-lock.yaml` — committed; `--frozen-lockfile`
  must reproduce it
- `src/admin-ui/eslint.config.js` — ESLint v9 flat
- `src/admin-ui/tsconfig.{json,app,node}.json` — strict TS
- `src/admin-ui/vite.config.ts` — build + Vitest config
- `src/admin-ui/vitest.contract.config.ts` — contract-test config
- `src/admin-ui/src/main.tsx`
- `src/admin-ui/src/app/App.tsx` — placeholder theme + router
- `src/admin-ui/src/stores/root.ts` — RootStore + AuthStore
  skeleton
- `src/admin-ui/src/lib/api/client.ts` — 401 redirect
- `src/admin-ui/src/types/errors.ts` — AppError model
- `src/admin-ui/src/types/api.generated.ts` — S3 codegen target
- `src/admin-ui/src/screens/login/LoginPage.tsx` — placeholder
- `src/admin-ui/src/screens/login/LoginPage.test.tsx` — smoke
- `src/admin-ui/{integration,tests/e2e,tests/contract,contracts}/`
- `src/admin-ui/README.md`
- `.github/workflows/ci-tests.yml` — backend + admin-ui jobs
- `Dockerfile` — admin-ui-build stage (unchanged but must still
  work against the new layout)
- `src/Dxs.Consigliere/Dxs.Consigliere.csproj` — `<AdminUiRoot>`
  publish target (unchanged but must still work)
- `docs/repository-zones/{zone-catalog,ownership-matrix}.md` +
  `.github/CODEOWNERS.template` — `admin-ui` zone

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Atomicity (A1 L4).** S0 was supposed to delete the
   legacy stub AND scaffold the new project in ONE commit so
   the Dockerfile + csproj publish path never sees a missing
   `package.json`. Verify `379b7c4` is the single commit
   that both removes and adds. Verify the Dockerfile
   `COPY ./src/admin-ui/package.json ./src/admin-ui/pnpm-lock.yaml`
   still works (files exist).

2. **CI integration (A1 H5).** `.github/workflows/ci-tests.yml`
   must:
   - Run on push to `main`, `codex-*`, AND `codex/*` (the
     current dev branch matches `codex/*`).
   - Have a separate `admin-ui` job with Node 22 + corepack +
     `pnpm install --frozen-lockfile && pnpm verify`.
   - Keep the existing backend `dotnet test` job intact.
   Flag any gap (e.g. missing branch filter, missing job
   dependency).

3. **Scripts contract (A1 M4 testing pyramid).** `package.json`
   must expose: `dev`, `build`, `typecheck`, `lint`, `test`
   (unit + integration via Vitest), `test:e2e` (Playwright),
   `test:contract` (Vitest with separate config), `verify`
   (typecheck → lint → test → build). Verify the file layout
   matches the master's Core Rule §15:
   - `unit` → `**/*.test.tsx`
   - `integration` → `src/admin-ui/integration/`
   - `e2e` → `src/admin-ui/tests/e2e/`
   - `contract` → `src/admin-ui/tests/contract/`

4. **Auth scaffold (A1 H2).** Verify:
   - `AppError` has both `Unauthorized` (401) AND `Forbidden`
     (403) categories with messages.
   - `ApiClient.request` actually checks `response.status ===
     401` and calls `window.location.assign("/login")` (but
     skips the redirect when already on `/login` to avoid a
     loop).
   - `/login` is reachable in the router (`App.tsx`).
   - The RootStore has an AuthStore slice with the
     `idle | loading | authenticated | anonymous | error`
     state machine.
   - Cookie auth: `credentials: "include"` on every fetch.

5. **Stack-profile alignment.** Re-verify the deps in
   `package.json` against `project-stack-profiles.md`
   §"Default Frontend Baseline":
   - React 19? Vite 7? TypeScript? MUI 7? MUI X DataGrid? MUI
     X Charts? MobX 6? mobx-persist-store? mobx-react-lite?
     react-router-dom 7? framer-motion? pnpm?
   Flag any version drift or omission. Note: `vitest` was
   bumped 2 → 3 during scaffold to satisfy vite-7 peer
   compatibility — verify this is acceptable (vitest is a
   devDep, not in the profile).

6. **TypeScript strictness.** `tsconfig.app.json` must enable:
   `strict`, `noUnusedLocals`, `noUnusedParameters`,
   `noFallthroughCasesInSwitch`, `noUncheckedSideEffectImports`.
   Flag any relaxation.

7. **ESLint config completeness.** Verify:
   - Flat config (v9 syntax).
   - `react`, `react-hooks`, `@typescript-eslint` plugins
     wired.
   - `react/react-in-jsx-scope` off (React 17+ JSX runtime).
   - `no-console` warn (frontend-principles §14 — security
     hygiene). Allow only `warn` / `error`.
   - `src/types/api.generated.ts` ignored (it's a codegen
     target).
   - `tests/e2e/**` ignored (Playwright has its own runner).

8. **Build output target.** `vite.config.ts` `build.outDir`
   points at `../Dxs.Consigliere/wwwroot` so the .NET
   publish path picks it up. Verify the Dockerfile and the
   `<AdminUiRoot>` target in `Dxs.Consigliere.csproj` both
   read from the same path.

9. **Zone catalog (A1 H1).** Verify:
   - `docs/repository-zones/zone-catalog.md` has an
     `admin-ui` row with path `src/admin-ui/**` + owner
     `operator/admin-ui` + backup `operator/api`.
   - `docs/repository-zones/ownership-matrix.md` has a
     matching task-type row.
   - `.github/CODEOWNERS.template` has `/src/admin-ui/` →
     `@replace-with-operator-admin-ui`.

10. **lockfile reproducibility.** `pnpm install
    --frozen-lockfile` must reproduce the committed
    `pnpm-lock.yaml` without changes. Flag any version
    drift, postinstall risk, or build-script approval issue
    (the install showed "Ignored build scripts: esbuild" —
    is that acceptable, or should `pnpm.onlyBuiltDependencies`
    be set in `package.json`?).

11. **Verify gate runs the right things.** `pnpm verify`
    chains `typecheck → lint → test → build`. Confirm:
    - It does NOT run `test:e2e` or `test:contract` (those
      are slower and have prerequisites).
    - `test` runs against jsdom (Vite config `test.environment
      = jsdom`).
    - All four commands ran green in the S0 commit message
      (`✓ typecheck / ✓ lint / ✓ test 2/2 / ✓ build 104 KB`).
    Spot-check: is the gzip 104 KB output reasonable for an
    empty shell with React + MUI placeholder theme, or is
    there a leak (e.g. icon font imported but unused)?

12. **Test isolation.** `LoginPage.test.tsx` is a render-only
    smoke. Verify it doesn't depend on global state (router
    context, MobX root). Verify it uses
    `@testing-library/react` properly (no `act` warnings).

13. **`api.generated.ts` placeholder.** Verify it's an empty
    re-export with a comment marking it as the S3 codegen
    target. Verify it's listed in `eslint.config.js`
    `ignores` (a codegen target shouldn't be linted).

14. **README freshness.** `src/admin-ui/README.md` is the
    canonical reference for future contributors. Does it
    accurately describe S0 state + reference the master.md
    + design bundle + the scripts? Anything stale or wrong?

15. **`/login` redirect loop guard.** `ApiClient` redirects
    401 to `/login` but skips when already on `/login`. Is
    the guard correct (`window.location.pathname !==
    "/login"`)? What about path edge cases (`/login?foo` or
    `/login/`)?

16. **No backend touched.** Verify S0 did NOT modify any C#
    source, Raven document, or admin REST endpoint. The UI
    program's product decision says backend contracts are
    consume-only.

17. **Dockerfile compatibility.** The Dockerfile copies
    `package.json` + `pnpm-lock.yaml` then runs
    `corepack enable && pnpm install --frozen-lockfile`.
    Verify our `packageManager: "pnpm@10.15.0"` is honoured
    by corepack on `node:22-alpine` (alpine sometimes lacks
    the corepack signing setup).

18. **Forward-compatibility.** S1 will write `theme.ts`, S2
    will wire the shell, S3 will add the SignalR client +
    event bus. Do the S0 placeholders correctly NOT block
    those moves (e.g. is the App.tsx structure conducive to
    a real Drawer-based shell, or will S2 have to throw it
    out)?

19. **Security hygiene (Core Rule §9, A1 M9).** S0 ships
    `no-console` warn in ESLint. Verify no `console.log`
    sneaked through. Verify the LoginPage placeholder does
    not echo any secret or token (it shouldn't — pure
    markup).

20. **Persistence skeleton.** `RootStore` mentions
    mobx-persist-store but doesn't wire it yet. Verify this
    is a deferred-to-S1 decision (S1 owns theme + density
    persistence with `persistVersion`) and not a missed S0
    step.

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
- Slice (S0 or program-level)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
