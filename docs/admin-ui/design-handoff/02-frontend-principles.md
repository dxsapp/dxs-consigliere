# Frontend Engineering Principles

## Purpose
A universal frontend engineering standard for any product or domain.
Project-specific stack baselines live in `project-stack-profiles.md`.
Detailed guidance for route-owned initial hydration lives in `frontend-route-driven-hydration.md`.
Detailed guidance for AI-first repository structure and continuation-friendly architecture lives in `frontend-ai-first-engineering.md`.
Detailed guidance for audit coverage and audit cadence lives in `frontend-audits-playbook.md`.

## Core Principles
1. Layered architecture: `UI -> state/use-cases -> API/infrastructure`.
2. Keep business logic out of UI components.
3. Use a single source of truth per screen/feature state.
4. Route all network calls through a shared API client.
5. Use typed contracts for DTOs and domain models.
6. Normalize HTTP/network/business errors into one error model.
7. Model async flows explicitly (`idle/loading/success/error`) or with state machines for complex flows.
8. For background activity (polling/timers/subscriptions), always implement cleanup, cancellation, and retry/backoff.
9. Build UI via design system primitives, theme, and tokens.
10. Reuse shared UI primitives (buttons, inputs, dialogs, toasts, headers).
11. Treat responsive behavior and accessibility as defaults (mobile-first, keyboard support, ARIA, contrast, touch targets).
12. On iPhone/WebView layouts, prefer a JS-synced `--doc-height` CSS variable over raw `100vh/100dvh` for stable viewport height.
13. Design for performance (lazy loading, code splitting, rerender control, memoization where justified).
14. Enforce client-side security hygiene (no secrets in client, sanitize input/output, avoid sensitive logs).
15. Include observability (structured error tracking and key UX event telemetry).
16. Initial business data hydration must not be owned by page-level `useEffect`; route-, shell-, or loader-level orchestration should call idempotent store entrypoints such as `ensureLoaded(...)`, while pages only render store state.
17. Use `useEffect` in pages for UI-local side effects, cleanup, or temporary integration glue, not as the steady-state owner of initial business data loading.
18. Keep logic testable with a clear testing pyramid (unit, integration, e2e for critical user paths).
19. Use a strict Definition of Done: `typecheck + lint + build + tests + manual QA` before merge.
20. Treat repository structure and code organization as **AI-First**: a new executor, including an AI agent, must be able to quickly find the bounded context, source of truth, store entrypoints, tests, and docs for any feature.
21. Optimize for continuation, not cleverness: prefer explicit, boring, repeated patterns that minimize context drift across streams and handoffs.
22. Treat documentation freshness as a quality and safety requirement: when runtime behavior, contracts, architecture, or operational rules change, the corresponding docs must be updated or explicitly marked stale.

## Product Runtime Addendum
1. Contract-first delivery: UI behavior must be grounded in explicit API, UX, and design contracts.
2. Deterministic behavior over implicit magic: avoid random IDs and hidden side effects in critical flows.
3. State lifecycle discipline: explicitly separate ephemeral, session, and persisted state, with clear reset rules.
4. UX safety rails for risky actions: pre-validation, confirmations, safe defaults, and clear recovery paths.
5. Documentation as a runtime asset: update rulebooks/handoff docs in the same PR as behavior changes.
6. Cross-stream ownership on shared code: shared changes require platform-level review and migration clarity.
7. Route-driven hydration over component lifecycle: repeated mounts, animations, or layout remounts must not cause duplicate network calls; stores must expose idempotent hydration APIs and own cache/hydration keys themselves.
8. AI-First architecture: each feature should be easy to localize, reason about, test, and hand off independently. Favor strong bounded contexts, explicit ownership, and consistent patterns across list/detail/analytics/runtime flows.
9. Auditability is a product property: auth, RBAC, contract drift, hydration, runtime operations, and doc freshness must be reviewable through explicit code paths and documented standards.

## Tooling and Repository Strategy
1. Standard package manager: `pnpm`.
2. `pnpm-lock.yaml` is the single lockfile authority; do not mix npm/yarn lockfiles.
3. Prefer monorepo when products share UI/platform code (example pattern: `apps/*` + `packages/*`).
4. Keep shared packages explicit and reusable (`ui`, `common`, API clients, config/tooling).
5. Shared package changes must be backward-compatible by default; breaking changes require migration notes.
6. Maintain a shared frontend snippets library (for example: `shared-snippets/frontend`) for reusable micro-patterns and utilities.
7. Before creating a new utility/helper, check the snippets library first and reuse/adapt existing patterns when possible.
8. When extracting stable logic from a product app into snippets, include context (`when to use`), usage notes, and constraints.

## Recommended Monorepo Structure
- `apps/<app>` - product applications.
- `packages/ui` - design system and reusable UI primitives.
- `packages/common` - shared domain/utils/config helpers.
- `packages/*-config` - lint/ts/build shared config packages.
- `docs` - contracts, rulebooks, handoff, architecture decisions.

## Team Operating Rules
1. One task = one focused change set.
2. Keep architecture decisions explicit and documented.
3. Prefer additive, backward-compatible changes.
4. Do not commit build artifacts.
5. Validate quality gates locally before opening/merging PR.
6. Keep docs current enough that a new executor can follow the current implementation rather than outdated intent.

## AI-First Engineering
1. Code, structure, and architecture must be optimized for safe continuation by a new executor, including an AI agent.
2. Keep features discoverable, predictable, local, testable, and handoff-friendly.
3. Prefer explicit ownership and boring architecture over clever indirection.
4. See `frontend-ai-first-engineering.md` for the full playbook.

## Audits
1. Frontend apps should maintain a real audit set covering security, data safety, UX, runtime truthfulness, and structural quality.
2. Audit selection and cadence should follow `frontend-audits-playbook.md`.
