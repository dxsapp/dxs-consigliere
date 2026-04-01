# JungleBus Block Sync Wave

## Goal

Make block synchronization follow the product posture that `Consigliere` now presents to operators:
- `Bitails websocket` for realtime observation
- `JungleBus / GorillaPool` for raw transaction fetch and block synchronization
- node RPC/ZMQ only as an advanced optional path

After this wave:
- block processing no longer depends on a valid node RPC URL as an unconditional prerequisite
- first-run setup explicitly configures JungleBus block sync
- setup cannot claim a healthy default bootstrap without block-sync configuration
- `BlockProcessContext.Messages` no longer fills with node-URI errors when node is not configured

## Problem Statement

Current block-processing flow still starts with node RPC header lookup before provider selection.
That means an invalid or placeholder `BsvNodeApi.BaseUrl` can break block synchronization even when product defaults point operators toward JungleBus and Bitails.

This creates two failures:
- runtime drift: block sync can stall even though the operator completed setup
- operator confusion: wizard hides the real block-sync dependency

## Product Decisions

Canonical first-run defaults after this wave:
- `realtime_ingest` = `Bitails websocket`
- `raw_tx_fetch` = `JungleBus / GorillaPool`
- `block_sync` = `JungleBus / GorillaPool`
- `rest_fallback` = `WhatsOnChain`

Advanced optional paths:
- node RPC/ZMQ remains available
- node is not required for default bootstrap
- node stays outside wizard v1 unless explicitly promoted later

## Scope

In scope:
- remove unconditional node-RPC dependency from block sync path
- add explicit JungleBus block-sync setup to first-run wizard
- validate block-sync prerequisites honestly in setup
- update provider/setup docs so capability mapping is clear
- add focused proof for block processing without node RPC

Out of scope:
- generic provider editor expansion
- node-first setup path
- full block-backfill strategy editor
- replacing all node-based validation/fetch paths
- changing non-block capabilities unless required by block-sync integration

## Zones

Primary zones:
- `indexer-ingest-orchestration`
- `public-api-and-realtime`
- `service-bootstrap-and-ops`
- `verification-and-conformance`
- `repo-governance`

Supporting zones:
- `external-chain-adapters`
- `frontend-admin-shell`

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `JB1` | `repo-governance` | `operator/governance` | `done` | - | docs review | block-sync capability contract is frozen |
| `JB2` | `indexer-ingest-orchestration` | `operator/runtime` | `done` | `JB1` | focused runtime tests | block processing no longer requires node RPC as unconditional first step |
| `JB3` | `external-chain-adapters` | `operator/integration` | `done` | `JB2` | adapter tests | JungleBus-backed block-sync path has the config it actually needs |
| `JB4` | `public-api-and-realtime` | `operator/api` | `done` | `JB1`,`JB2`,`JB3` | setup API tests | setup contract explicitly includes JungleBus block-sync inputs |
| `JB5` | `frontend-admin-shell` | `operator/ui` | `done` | `JB4` | frontend build + smoke | wizard has an explicit JungleBus block-sync step or substep |
| `JB6` | `service-bootstrap-and-ops` | `operator/platform` | `done` | `JB2`,`JB4`,`JB5` | runtime diagnostics review | startup/runtime surfaces stop implying node is required for default block sync |
| `JB7` | `verification-and-conformance` | `operator/verification` | `done` | `JB2`,`JB3`,`JB4`,`JB5`,`JB6` | focused proof | invalid node RPC no longer poisons block sync and setup contract stays coherent |
| `A1` | `repo-governance` | `operator/governance` | `done` | `JB7` | audit | JungleBus-first block sync posture is closed honestly |

## Expected File Targets

Runtime and orchestration:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockProcessExecutor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/ActualChainTipVerifyBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/AppInitBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/JungleBusBlockchainDataProvider.cs`

Setup/API/UI:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/Setup/SetupStatusResponse.cs`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/SetupPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/setup.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/api.ts`

Docs:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/junglebus-block-sync-wave/*`

## Hard Boundaries

- do not leave unconditional `rpcClient.GetBlockHeader(...)` as the first step in default block sync path
- do not let wizard claim setup is complete while JungleBus block-sync prerequisites are missing
- do not broaden the wizard into a generic provider control panel
- do not remove node support entirely; keep it as advanced optional infrastructure
- do not hide restart/apply semantics if runtime wiring still needs restart

## Definition of Done

- invalid or placeholder `BsvNodeApi.BaseUrl` no longer breaks default block synchronization
- first-run setup explicitly configures JungleBus block sync
- setup defaults are coherent with runtime behavior
- block-sync capability is explained clearly in product docs/UI
- focused tests prove no more node-URI poison path in `BlockProcessContext`
