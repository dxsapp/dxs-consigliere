# Provider Health And Entity Ops Surface Wave

## Goal

Add the missing operator read surface for:
- JungleBus block-sync health and lag
- richer address detail stats
- richer token detail stats

After this wave, an operator should be able to answer three practical questions from the admin UI without digging through Raven or logs:
- is JungleBus block sync healthy and how far behind is it?
- what is the current tracked state for this address?
- what is the current tracked state for this token?

## Problem Statement

Current admin details are too thin for real operator work.
The provider page explains configuration, but it does not show live JungleBus block-sync health or lag.
Tracked address/token detail endpoints mostly expose lifecycle/readiness metadata, but not the operational counters and state summaries an operator expects.

That leaves the admin shell unable to answer the most common runtime questions:
- current UTXO count
- balance snapshot
- transaction count
- first/last seen transaction timing
- JungleBus observed tip vs local indexed tip

## Scope

In scope:
- JungleBus block-sync health read model and admin endpoint(s)
- address detail operational summary
- token detail operational summary
- admin UI cards/panels for these stats
- clear fallback behavior when data is not yet available

Out of scope:
- generic config editing
- changing provider selection logic
- new realtime transports
- historical backfill algorithm changes
- entity mutation workflow changes beyond existing actions

## Product Decisions

### JungleBus ops panel must show
- block sync enabled/configured state
- block subscription id presence
- last observed JungleBus block height
- highest known local indexed block height
- lag in blocks
- last control message timestamp
- last successful sync scheduling or processing timestamp
- last error if any

### Address details must show
- current BSV balance in local view
- current tracked token balances summary
- UTXO count
- transaction count
- first transaction timestamp and/or block height
- last transaction timestamp and/or block height
- readiness/degraded/failure summary

### Token details must show
- scoped balance/supply summary as available in local state
- holder count if derivable cheaply
- UTXO count
- transaction count
- first transaction timestamp and/or block height
- last transaction timestamp and/or block height
- rooted history/trusted-roots/unknown-root summary

### UI position
- JungleBus health belongs in runtime/ops diagnostics
- address/token stats belong on their existing detail pages
- do not overload `/providers` with live runtime diagnostics beyond provider cards

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
| `PH1` | `repo-governance` | `operator/governance` | `todo` | - | docs review | ops surface contract and metrics are frozen |
| `PH2` | `indexer-state-and-storage` | `operator/state` | `todo` | `PH1` | query tests | address/token operational summary read models exist |
| `PH3` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `PH1` | runtime tests | JungleBus block-sync health/lag read model exists |
| `PH4` | `public-api-and-realtime` | `operator/api` | `todo` | `PH2`,`PH3` | controller tests | admin endpoints expose the new summaries clearly |
| `PH5` | `frontend-admin-shell` | `operator/ui` | `todo` | `PH4` | frontend build + page QA | runtime/address/token surfaces render the new ops summaries well |
| `PH6` | `verification-and-conformance` | `operator/verification` | `todo` | `PH2`,`PH3`,`PH4`,`PH5` | focused proof | sample operator questions can be answered from the UI/API |
| `A1` | `repo-governance` | `operator/governance` | `todo` | `PH6` | audit | missing ops visibility is closed honestly |

## Expected File Targets

State and read models:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tracking/AdminTrackingQueryService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/Admin/AdminTrackedAddressResponse.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/Admin/AdminTrackedTokenResponse.cs`
- new DTO/read-model support under `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/**`

Runtime ops:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/OpsController.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/Admin*.cs`

Frontend:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/AddressDetailPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/TokenDetailPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/api.ts`

Docs:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/provider-health-and-entity-ops-surface/*`

## Hard Boundaries

- do not turn `/providers` into a runtime dashboard
- do not add heavyweight aggregated queries that will obviously hurt Raven under normal operator use
- keep the read model capability-focused and operator-facing
- prefer explicit unavailable/unknown states over fake zeros
- do not mix this wave with new mutation flows unless a read surface truly depends on it

## Definition of Done

- admin runtime screen shows JungleBus block-sync health and lag clearly
- address detail page shows operational stats an operator actually needs
- token detail page shows operational stats an operator actually needs
- API contracts are explicit and stable
- focused tests prove the summaries are coherent and cheap to read
