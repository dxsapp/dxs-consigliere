# Admin UI Operator Utility Wave

## Goal

Turn the current admin shell into an operator-useful `v1` surface.

After this wave, an operator should be able to open the admin UI and quickly understand:
- what is tracked
- what is unhealthy or degraded
- what actions are available now
- what data is authoritative inside the current managed scope
- what remains scoped, unavailable, or intentionally deferred

## Core Decision

This wave optimizes for operator usefulness, not generic dashboard polish.

The UI should prefer:
- current managed state over debug dumps
- action-first surfaces over passive data walls
- readiness and operational meaning over raw backend shape
- scoped-history honesty over fake completeness

The admin shell is not a block explorer, not a raw JSON console, and not the future `v2` History workspace.

## Scope

In scope:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/components/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/**`
- small supporting API/DTO additions only when the frontend cannot become useful without them
- admin UI copy/docs alignment where the wave materially changes operator behavior

Out of scope:
- dedicated `v2` History section
- broad backend refactors unrelated to current admin usefulness
- visual redesign for its own sake
- explorer-grade token or address history UX
- generic config editor behavior

## Core Rules

- Do not surface raw JSON as the primary content of an operator page.
- Do not duplicate the same operational story across `Dashboard`, `Runtime`, `Storage`, and `Providers`.
- Do not imply unlimited history or cross-checked assurance where the product does not actually provide it.
- Prefer summary cards, status rows, and explicit actions over low-signal technical dumps.
- If a screen needs additional backend fields to become useful, add the smallest contract necessary.
- Keep `Setup` first-run oriented, `Providers` advanced-config oriented, and `Runtime` diagnostics oriented.

## Ownership Zones

| slice | zone lead | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `AU1` | `public-api-and-realtime` | `operator/api` | `todo` | - | frontend build + page contract review | page roles and information hierarchy are frozen |
| `AU2` | `public-api-and-realtime` | `operator/api` | `todo` | `AU1` | frontend build + manual page smoke | dashboard/runtime/storage/providers surfaces stop fighting each other |
| `AU3` | `public-api-and-realtime` | `operator/api` | `todo` | `AU1` | frontend build + entity-page smoke | address/token list and detail pages show operator-useful summaries and actions |
| `AU4` | `public-api-and-realtime` | `operator/api` | `todo` | `AU2`,`AU3` | frontend build + copy review | remaining raw dumps, legacy wording, and ambiguous state labels are removed |
| `AU5` | `verification-and-conformance` | `operator/verification` | `todo` | `AU2`,`AU3`,`AU4` | `pnpm typecheck`, `pnpm build`, manual admin smoke | wave proof and residuals are captured honestly |

## Definition of Done

- `Dashboard` answers “what matters right now?” without sending the operator to raw JSON.
- `Runtime` answers “what is degraded, stalled, or blocked?” with clear status and no low-signal dumps.
- `Storage` answers “what storage posture is active?” using readable status surfaces rather than raw objects.
- `Providers` answers “what is configured, recommended, and active?” without drowning the operator in duplicated detail.
- `Addresses` and `Tokens` list/detail pages surface current state, readiness, and useful actions clearly.
- Any remaining technical detail is subordinate and collapsible, not the main UI.
- `pnpm typecheck` and `pnpm build` pass.
- The admin shell feels like an operator console for the current product, not a partially dressed API browser.

## Delivery Notes

- Package path: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/`
- Record implementation commit hashes here as slices land.
- If small API/DTO additions become necessary, add a handoff note in this file before crossing zones.
