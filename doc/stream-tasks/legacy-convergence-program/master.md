# Legacy Convergence Program

## Goal

Converge the old and new `Consigliere` models into one consistent product, runtime, and operator contract.

After this program:
- blockchain capabilities are named and wired consistently
- provider roles are understandable to operators
- `Consigliere` is explicitly the authoritative local `(D)STAS` validation engine
- providers are clearly positioned as data sources, not token-truth engines
- setup, providers, runtime diagnostics, and runtime behavior all follow the same mental model

## Canonical Product Model

### Provider posture

- `Bitails` = default `realtime_ingest`
- `JungleBus / GorillaPool` = default `block_sync` and preferred `raw_tx_fetch`
- `WhatsOnChain` = `REST fallback`

### Validation authority

`Consigliere` is the authoritative local validation engine for `(D)STAS`.

Providers may supply:
- raw transactions
- realtime observations
- block progression
- dependency data

Providers do not supply:
- authoritative `(D)STAS` legality verdicts
- trusted-root decisions
- rooted canonical token truth

### Rooted token truth

Authoritative token history is:
- lineage-aware
- rooted to explicit `trustedRoots[]`
- bounded by the managed-scope rooted-history model

This means:
- `valid root` and `trusted root` are not the same thing
- `unknown root` and `illegal root` are not the same thing
- rooted history is not generic explorer-style token history

## Canonical Capability Inventory

### First-class operator-visible capabilities

- `realtime_ingest`
- `block_sync`
- `raw_tx_fetch`
- `validation_fetch`
- `historical_token_scan`
- `historical_address_scan`
- `broadcast`

### Capability meanings

#### `realtime_ingest`
- live transaction and chain observation intake
- default source: `Bitails websocket`

#### `block_sync`
- block progression and block-driven catch-up
- default source: `JungleBus`

#### `raw_tx_fetch`
- acquire raw transaction hex by txid
- preferred source: `JungleBus / GorillaPool`

#### `validation_fetch`
- acquire lineage-critical dependency data needed for `Consigliere`'s local `(D)STAS` validation engine
- this is not external validation authority

#### `historical_token_scan`
- rooted token-history expansion inside the trusted-root universe
- this is not generic explorer-style token discovery

#### `historical_address_scan`
- address history acquisition for tracked scope

#### `broadcast`
- push raw tx to every configured broadcast-capable provider
- overall success rule: `any_success`

## Program Position

This is not a single large refactor.

It is a bounded convergence program that should:
- remove semantic drift first
- unify the most operator-visible flows next
- delete dead legacy last

The program should preserve a working runtime after each wave.

## Routing And Ownership

Primary zones:
- `repo-governance`
- `service-bootstrap-and-ops`
- `indexer-ingest-orchestration`
- `indexer-state-and-storage`
- `public-api-and-realtime`
- `verification-and-conformance`

Supporting zone:
- `external-chain-adapters`

## Program Ledger

| wave | zone lead | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `LC1` Capability Contract Cleanup | `repo-governance` | `operator/governance` | `done` | - | docs review + frontend build | docs, config naming, and operator wording distinguish `raw_tx_fetch`, `validation_fetch`, and rooted-history semantics correctly |
| `LC2` Raw Tx Convergence | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `LC1` | focused runtime tests | all raw-transaction consumers route through one internal raw-tx contract |
| `LC3` Validation Capability Convergence | `indexer-state-and-storage` | `operator/state` | `todo` | `LC1`,`LC2` | validation tests + API proof | local `(D)STAS` validation semantics, dependency acquisition, and public wording are aligned |
| `LC4` Historical Scan Truthfulness | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `LC1` | docs + runtime proof | historical scans are either honestly scoped as v1-specific or routed consistently |
| `LC5` Broadcast Multi-Target | `public-api-and-realtime` | `operator/api` | `todo` | `LC1` | controller/service tests | broadcast uses multi-target `any_success` semantics and exposes attempt truth honestly |
| `LC6` Dead Legacy Removal | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `LC2`,`LC3`,`LC4`,`LC5` | build + diff hygiene | stale config fields, stale docs, and bypass wiring that contradict the canonical model are removed |
| `A1` Program Closeout Audit | `repo-governance` | `operator/governance` | `todo` | `LC6` | audit | one coherent capability-first model exists across docs, code, runtime, and admin UI |

## Hard Boundaries

- do not collapse `raw_tx_fetch`, `validation_fetch`, and `historical_token_scan` into one capability
- do not describe providers as validation authorities
- do not describe rooted token history as generic token history
- do not leave user-facing capability knobs that are not wired end-to-end
- do not remove `validation_fetch`; clarify and strengthen it instead

## Definition Of Done

- one capability-first model exists across docs, runtime wiring, and admin UI
- `validation_fetch` is preserved and described correctly as local-validation support
- `Consigliere` is clearly the authoritative `(D)STAS` validation engine
- raw-tx routing is unified
- historical scan semantics are honest
- broadcast semantics match product policy
- dead legacy that contradicts the model is removed
