# Program Slices

## `LC1` Capability Contract Cleanup

Freeze the canonical meaning of the main blockchain capabilities and remove wording drift.

Must distinguish explicitly:
- `raw_tx_fetch`
- `validation_fetch`
- `historical_token_scan`
- rooted token-history semantics

Must state clearly:
- providers supply data
- `Consigliere` supplies `(D)STAS` truth
- `validation_fetch` supports local validation by acquiring missing dependencies

Likely docs and surfaces:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-capability-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/root-semantics-glossary.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`

## `LC2` Raw Tx Convergence

Create one internal raw-transaction acquisition contract and move all raw-tx consumers behind it.

Rules:
- one internal routed service for raw transaction hex acquisition
- no scattered direct provider calls for ordinary raw-tx use cases
- preferred source remains `JungleBus / GorillaPool`
- fallback behavior must be explicit and testable

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/**`

Likely consumers:
- missing-transaction fetch
- repair flows
- historical hydration paths where raw tx is required

## `LC3` Validation Capability Convergence

Elevate `validation_fetch` into a clean first-class support capability for local `(D)STAS` validation.

Must preserve:
- public validation endpoint
- local lineage-aware validation authority

Must define:
- what dependency data `validation_fetch` may acquire
- when dependency acquisition happens
- how it interacts with local derived transaction state
- how public validation results map to:
  - `illegal root`
  - `unknown root`
  - `trusted root`
  - `B2G resolved`

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/TransactionQueryService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/ValidateStasResponse.cs`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/**`

## `LC4` Historical Scan Truthfulness

Make historical scan semantics honest and operator-understandable.

Decision requirement:
- either explicitly freeze `historical_address_scan` and `historical_token_scan` as current v1-specific provider-backed flows
- or move them behind generalized capability routing

Recommendation:
- make the semantics truthful first
- generalize only after truthfulness is achieved

Must answer:
- what historical scans actually affect
- whether token scans are rooted-universe-only
- whether address scans are provider-specific in v1

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/history-sync-model.md`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/HistoricalAddressBackfillRunner.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/HistoricalTokenBackfillRunner.cs`

## `LC5` Broadcast Multi-Target

Bring broadcast semantics in line with declared product policy.

Must implement:
- send to all configured broadcast-capable providers in parallel
- overall success = `any_success`
- overall failure = `all_failed`

Must expose honestly:
- per-provider attempts
- aggregate success/failure verdict

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/BroadcastService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Configs/**`
- related API DTOs and controller tests

## `LC6` Dead Legacy Removal

Remove old config semantics, wording, and bypass wiring that contradict the canonical model.

Targets:
- stale capability names
- stale provider docs and examples
- dead config fields
- direct provider wiring that bypasses new routing/contracts
- legacy wording that implies providers validate `(D)STAS`

Rules:
- remove only after replacement path is already live
- keep runtime working after each deletion step

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Configs/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/**`
- `/Users/imighty/Code/dxs-consigliere/doc/**`
- selected legacy runtime services

## `A1` Program Closeout Audit

Close out the program with an honest audit.

Must answer:
- is there one coherent capability-first mental model now?
- is `validation_fetch` preserved and described correctly?
- are raw tx, validation, and historical scan flows still semantically mixed anywhere?
- do docs, runtime behavior, and admin UI now agree?

Closeout artifacts:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/legacy-convergence-program/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/legacy-convergence-program/evidence/closeout.md`

