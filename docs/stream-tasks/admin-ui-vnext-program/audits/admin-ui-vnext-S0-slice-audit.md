# Admin UI vNext — S0 Slice Audit

Target commit: `379b7c4` (`feat(admin-ui): wave-vnext S0 — atomic scaffold + CI + auth shell`)
Reviewer: GPT-5 Codex
Date: 2026-05-18

Dimensions covered:
1. Atomicity (A1 L4)
2. CI integration (A1 H5)
3. Scripts contract (A1 M4 testing pyramid)
4. Auth scaffold (A1 H2)
5. Stack-profile alignment
6. TypeScript strictness
7. ESLint config completeness
8. Build output target
9. Zone catalog (A1 H1)
10. Lockfile reproducibility
11. Verify gate runs the right things
12. Test isolation
13. `api.generated.ts` placeholder
14. README freshness
15. `/login` redirect loop guard
16. No backend touched
17. Dockerfile compatibility
18. Forward-compatibility
19. Security hygiene
20. Persistence skeleton

## Scope

Audited the S0 deliverable at `379b7c4` against:

- `docs/stream-tasks/admin-ui-vnext-program/master.md`
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-audit-A1-followup.md`
- `docs/stream-tasks/admin-ui-vnext-program/audits/admin-ui-vnext-audit-A1-pass2.md`
- `/Users/imighty/Code/docs/project-stack-profiles.md`
- `/Users/imighty/Code/docs/frontend-principles.md`
- `/Users/imighty/Code/docs/frontend-audits-playbook.md`
- Actual S0 files under `src/admin-ui/**`
- `.github/workflows/ci-tests.yml`
- `.github/workflows/docker-release.yml`
- `src/Dxs.Consigliere/Dxs.Consigliere.csproj`
- `docs/repository-zones/{zone-catalog,ownership-matrix}.md`
- `.github/CODEOWNERS.template`

## Validation

- `git show -s --format=fuller 379b7c4`
- `git show --stat --name-status --oneline 379b7c4 --`
- `git diff --name-status 379b7c4^ 379b7c4 -- src/admin-ui`
- `git diff --name-only 379b7c4^ 379b7c4 -- 'src/Dxs.Consigliere/**/*.cs' 'src/Dxs.Consigliere/**/*.csproj' 'src/Dxs.Consigliere/**'`
- `corepack prepare pnpm@10.15.0 --activate && pnpm --version && pnpm install --frozen-lockfile`
- `pnpm verify`

The frontend verify gate is reproducible in this workspace after activating the pinned package manager:

- `pnpm install --frozen-lockfile` completed without lockfile changes.
- `pnpm verify` passed: typecheck, lint, Vitest 2/2, and Vite build.
- Build output: `../Dxs.Consigliere/wwwroot/assets/index-*.js` at `319.13 kB` raw / `104.29 kB` gzip, consistent with the S0 commit message.

## Checks That Passed

- Atomic scaffold: the S0 commit deletes the legacy admin UI files and adds the new Vite scaffold in the same commit. `src/admin-ui/package.json` and `src/admin-ui/pnpm-lock.yaml` exist in the target tree.
- CI: `.github/workflows/ci-tests.yml` runs on `main`, `codex-*`, and `codex/*`, preserves the backend `.NET` job, and adds an `admin-ui` job using Node 22, corepack, frozen pnpm install, and `pnpm verify` (`ci-tests.yml:3-56`).
- Scripts: `package.json` exposes `dev`, `build`, `typecheck`, `lint`, `test`, `test:e2e`, `test:contract`, and `verify`; `verify` chains typecheck → lint → test → build and excludes e2e/contract by design (`package.json:6-17`).
- Stack: dependencies align with the default frontend baseline: React 19, Vite 7, TypeScript, MUI 7, MUI X DataGrid/Charts, MobX, `mobx-persist-store`, `mobx-react-lite`, `react-router-dom` 7, `framer-motion`, and pnpm (`package.json:19-55`).
- TypeScript: `tsconfig.app.json` enables `strict`, `noUnusedLocals`, `noUnusedParameters`, `noFallthroughCasesInSwitch`, and `noUncheckedSideEffectImports`.
- ESLint: flat config uses `@eslint/js`, `typescript-eslint`, `react`, `react-hooks`, disables `react/react-in-jsx-scope`, warns on `no-console` while allowing `warn/error`, and ignores `src/types/api.generated.ts` and `tests/e2e/**`.
- Auth scaffold: `AppError` includes `Unauthorized` and `Forbidden`; `ApiClient` sends `credentials: "include"`, redirects 401 to `/login`, skips redirect when already on `/login`, throws `Forbidden` for 403, and `/login` is routed in `App.tsx` (`client.ts:40-65`, `App.tsx:21-34`).
- Test isolation: `LoginPage.test.tsx` renders the placeholder directly with Testing Library and has no router/MobX dependency.
- Codegen placeholder: `src/types/api.generated.ts` is an empty S3 codegen target and is excluded from ESLint.
- No backend source was touched by S0: the S0 diff has no C# source, Raven model, REST controller, or `.csproj` changes under `src/Dxs.Consigliere/**`.
- Zone docs: `admin-ui` exists in the zone catalog, ownership matrix, and CODEOWNERS template (`zone-catalog.md:15`, `ownership-matrix.md:15`, `.github/CODEOWNERS.template:13`).
- Persistence skeleton: S0 correctly defers `mobx-persist-store` wiring to S1; `RootStore` only carries the auth/API skeleton (`root.ts:17-40`).

## Findings

### H1

- Severity: HIGH
- Slice: S0
- Issue: The S0 commit claims Dockerfile compatibility, and the release workflow still builds `file: ./Dockerfile`, but root `Dockerfile` is not tracked in the target tree. `git cat-file -e 379b7c4:Dockerfile` fails, while `.github/workflows/docker-release.yml:75-76` points Docker Buildx at `./Dockerfile`; a clean GitHub checkout of `379b7c4` will not have the untracked local Dockerfile that exists on this machine.
- Recommended fix: Add the root `Dockerfile` to git in the S0 followup, or update `docker-release.yml` to point to a tracked Dockerfile. Then run/record a Docker build smoke (`docker build -f ./Dockerfile .` or equivalent CI proof) so the admin-ui build stage is verified in a clean checkout.

### L1

- Severity: LOW
- Slice: S0
- Issue: `src/admin-ui/README.md` has stale S0 metadata: it says package manager is `pnpm 9` (`README.md:28`) while `package.json` pins `pnpm@10.15.0` (`package.json:55`), and it says CI runs on `main` or `codex-*` only (`README.md:88-90`) while the workflow also includes `codex/*` (`ci-tests.yml:3-9`). The stack-profile link label points at `/Users/imighty/Code/docs/project-stack-profiles.md` but the actual relative target is an uploaded bundle path (`README.md:4-5`).
- Recommended fix: Update the README to say `pnpm 10.15.0`, list `main`, `codex-*`, and `codex/*`, and link either to the actual workspace baseline path as plain text or to a valid repo-local mirror.

### L2

- Severity: LOW
- Slice: program-level
- Issue: The approved plan's Core Rule 15 says integration tests live in `src/admin-ui/src/integration/` (`master.md:193-199`), but S0 creates and configures top-level `src/admin-ui/integration/` (`vite.config.ts:34-39`, `README.md:73`). The S0 audit prompt also expects the top-level path, so implementation is internally consistent, but the master plan remains stale.
- Recommended fix: Align `master.md` Core Rule 15 with S0 by changing the integration-test location to `src/admin-ui/integration/`.

## Residual Risk

The only material blocker is the missing tracked root Dockerfile. The admin UI scaffold itself is otherwise suitable for S1: S1 can replace the placeholder theme, S2 can replace `AuthedPlaceholder` with the Drawer/AppBar shell, and S3 can extend the API/auth/event-bus skeleton without fighting S0 structure.

Verdict: APPROVE WITH CHANGES
Critical findings: 0
High findings: 1
Medium findings: 0
Low findings: 2
Headline: S0 frontend scaffold and CI gate are sound, but the release Dockerfile path is not valid in a clean checkout until the root Dockerfile is tracked or the workflow is repointed.
