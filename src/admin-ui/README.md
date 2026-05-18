# Consigliere Admin UI

The operator surface for the Consigliere BSV thin-node observer.
Built on the workspace-default frontend stack per
[`/Users/imighty/Code/docs/project-stack-profiles.md`](../../docs/admin-ui/design-bundle/project/uploads/01-stack-profile.md):
React 19 + Vite 7 + TypeScript + MUI + MUI X + MobX + framer-motion.

## Status

**S0 scaffold** — atomic delete + new project + CI integration +
auth-aware skeleton landed in one commit per the
[program plan](../../docs/stream-tasks/admin-ui-vnext-program/master.md).
S1-S12 ship the theme, shell, API/SignalR/mock layer, 14 screens,
and quality gates.

## Stack

| Concern | Choice |
|---|---|
| Build | Vite 7 |
| UI | MUI 7 + MUI X DataGrid + MUI X Charts |
| State | MobX 6 + mobx-react-lite + mobx-persist-store |
| Routing | react-router-dom 7 |
| Animations | framer-motion |
| Tests (unit/integration) | Vitest + Testing Library |
| Tests (e2e) | Playwright |
| Tests (contract) | Vitest against a live ASP.NET host |
| Package manager | pnpm 9 |

## Scripts

| Command | What |
|---|---|
| `pnpm dev` | Vite dev server on :5173 with `/api` proxied to `VITE_API_ORIGIN` (default `http://localhost:5000`) |
| `pnpm build` | `tsc -b` then Vite production build into `../Dxs.Consigliere/wwwroot` |
| `pnpm typecheck` | TS strict check across `src/`, `integration/`, `tests/` |
| `pnpm lint` | ESLint v9 flat config |
| `pnpm test` | Vitest unit + integration |
| `pnpm test:e2e` | Playwright golden-path e2e (S12) |
| `pnpm test:contract` | Backend payload parity against a live ASP.NET host (S3) |
| `pnpm verify` | typecheck → lint → test → build (CI gate) |

## Layout (S0 — fills in over S1-S10)

```
src/admin-ui/
├── package.json
├── vite.config.ts            # build + Vitest unit/integration
├── vitest.contract.config.ts # contract-parity tests (separate)
├── eslint.config.js          # ESLint v9 flat config
├── tsconfig.{json,app,node}.json
├── index.html
├── contracts/                # S3: committed swagger.json
├── src/
│   ├── main.tsx
│   ├── app/
│   │   ├── App.tsx           # ThemeProvider + Router
│   │   └── theme.ts          # S1 fills with bundle tokens
│   ├── stores/
│   │   └── root.ts           # root store; event bus lands in S3
│   ├── lib/
│   │   ├── api/client.ts     # fetch wrapper with 401-redirect
│   │   ├── auth-client/      # S3
│   │   ├── signalr-client/   # S3
│   │   └── mock/             # S3
│   ├── types/
│   │   ├── api.generated.ts  # S3: codegen target
│   │   ├── domain.ts         # S2+
│   │   └── errors.ts
│   ├── screens/
│   │   └── login/            # S0 placeholder; real form in S2
│   └── test-setup.ts
├── integration/              # S3+: store + mock API integration tests
└── tests/
    ├── e2e/                  # S12: Playwright
    └── contract/             # S3: parity vs live ASP.NET
```

## API mode

| Mode | Behaviour |
|---|---|
| `VITE_API_MODE=real` (default) | Real ASP.NET backend; cookie auth; SignalR connects |
| `VITE_API_MODE=mock` | All endpoints served by `src/lib/mock/` with seed data mirroring real DTO shape (per Core Rule §12) |

## CI

GitHub Actions workflow `.github/workflows/ci-tests.yml` runs
`pnpm install --frozen-lockfile && pnpm verify` from this directory
on every push to `main` or `codex-*` branches.

## Design reference

The visual contract this UI implements:

- [`docs/admin-ui/design-handoff/00-design-brief.md`](../../docs/admin-ui/design-handoff/00-design-brief.md)
- [`docs/admin-ui/design-bundle/`](../../docs/admin-ui/design-bundle/) — hi-fi mockups + theme.jsx tokens

## Program tracking

- [Program master](../../docs/stream-tasks/admin-ui-vnext-program/master.md)
- [Audit trail](../../docs/stream-tasks/admin-ui-vnext-program/audits/)
- [Closeout (S12)](../../docs/stream-tasks/admin-ui-vnext-program/evidence/)
