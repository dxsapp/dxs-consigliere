# JungleBus Chain Tip Assurance Wave

## Goal

Add a dedicated JungleBus-first chain-tip assurance surface so operators can distinguish:
- normal observed-vs-local block lag
- stalled JungleBus control flow
- missing external chain-tip confidence in JungleBus-first mode

This wave is explicitly about read-only assurance and diagnostics.
It must not reintroduce node RPC as a required dependency for block sync.

## Problem Statement

Current JungleBus health only shows:
- observed JungleBus block height
- highest known local block height
- derived lag
- last control/scheduled/processed markers

That is useful but insufficient as an assurance surface.
It does not answer:
- is the JungleBus tip still moving?
- is the local chain stuck while JungleBus advances?
- is assurance degraded because we have no secondary chain-tip cross-check?
- are we stale because control messages stopped entirely?

In JungleBus-first mode we intentionally removed node-required verification, but we still need an honest operator-grade confidence model.

## Scope

In scope:
- JungleBus-first chain-tip assurance read model
- explicit confidence/degraded/stale states for `/runtime`
- API contract for chain-tip assurance payload
- admin UI card/panel for assurance
- clear unavailable semantics when JungleBus is not the active block-sync path

Out of scope:
- restoring node-required verification
- changing provider selection or block sync routing
- generic alerting framework
- paging/notification automation
- reorg repair logic changes

## Product Decisions

### Assurance questions the UI must answer
- is JungleBus block-sync currently the active source of truth for block movement?
- when was the last observed JungleBus tip movement?
- when was the last local indexed block movement?
- is the local index catching up, stalled, or drifting?
- do we currently have only single-source assurance, or a secondary cross-check?

### Core operator states
- `healthy`
- `catching_up`
- `stalled_control_flow`
- `stalled_local_progress`
- `degraded_single_source`
- `unavailable`

### Secondary assurance model
- in JungleBus-first mode, if node verification is absent, UI must say that assurance is single-source, not pretend full external confirmation exists
- if a secondary source exists later, it can upgrade the assurance state, but this wave must work honestly without it

## Zones

Primary zones:
- `indexer-state-and-storage`
- `indexer-ingest-orchestration`
- `public-api-and-realtime`
- `frontend-admin-shell`
- `verification-and-conformance`
- `repo-governance`

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `JA1` | `repo-governance` | `operator/governance` | `todo` | - | docs review | assurance contract and states are frozen |
| `JA2` | `indexer-state-and-storage` | `operator/state` | `todo` | `JA1` | query tests | persisted/read-only assurance snapshot exists |
| `JA3` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `JA1`,`JA2` | runtime tests | JungleBus runtime updates assurance fields honestly |
| `JA4` | `public-api-and-realtime` | `operator/api` | `todo` | `JA2`,`JA3` | controller tests | ops endpoint exposes assurance payload clearly |
| `JA5` | `frontend-admin-shell` | `operator/ui` | `todo` | `JA4` | frontend build + page QA | runtime page shows assurance state clearly |
| `JA6` | `verification-and-conformance` | `operator/verification` | `todo` | `JA2`,`JA3`,`JA4`,`JA5` | focused proof | stale/control/local-progress scenarios are covered |
| `A1` | `repo-governance` | `operator/governance` | `todo` | `JA6` | audit | JungleBus-first assurance gap is closed honestly |

## Expected File Targets

Runtime/state:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Runtime/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/**`

API:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/OpsController.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/**`

Frontend:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/ops.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/api.ts`

Docs:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/junglebus-chain-tip-assurance-wave/*`

## Hard Boundaries

- do not make node RPC mandatory again
- do not turn assurance state into a fake consensus guarantee
- prefer explicit degraded/single-source messaging over optimistic labels
- keep this wave read-only and diagnostics-first

## Definition of Done

- runtime exposes a dedicated JungleBus chain-tip assurance payload
- UI distinguishes lag from assurance confidence
- stalled control flow and stalled local progress are explicit states
- JungleBus-first mode is marked as single-source assurance when no secondary verification exists
- operators can tell whether the index is healthy, catching up, or stale without reading logs
