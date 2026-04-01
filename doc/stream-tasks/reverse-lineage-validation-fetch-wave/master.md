# Reverse Lineage Validation Fetch Wave

## Goal

Make `validation_fetch` use a bounded JungleBus `getRawTx` reverse-lineage strategy for `(D)STAS` validation repair.

The strategy is intentionally narrow:
- start from one known transaction
- walk backward through parent inputs
- stop as soon as validation/root semantics allow a verdict boundary
- do not use this path as a generic historical discovery engine

## Product Decision

### Canonical v1 policy
- `validation authority` remains local to `Consigliere`
- `validation_fetch` primary upstream path becomes `JungleBus transaction/get`
- traversal mode becomes `reverse_lineage`
- rate limit is capped at `10 requests/second`

### Scope boundary
This wave is for:
- B2G dependency repair
- lineage completion
- rooted validation support

This wave is not for:
- broad token discovery
- address history discovery
- full explorer-style historical expansion

## Why this wave exists

The new validation repair subsystem now exists, but it still treats dependency acquisition as generic tx fetching.

For `(D)STAS` lineage repair, the cheapest useful strategy is:
1. start from the unresolved tx
2. fetch parent tx hex via JungleBus `getRawTx`
3. parse locally
4. continue walking backward only where lineage requires it
5. stop immediately on trusted/valid/illegal boundary

That is materially cheaper than broad scans and matches rooted-history semantics.

## Canonical semantics

### `raw_tx_fetch`
Simple tx-by-id acquisition.

### `validation_fetch`
Bounded dependency acquisition for local authoritative validation.

### `reverse_lineage`
Validation-fetch mode that walks backward from one known tx through its ancestry only.

## Hard rules

- keep `JungleBus getRawTx` throttled to `10 req/sec`
- maintain a visited set per repair so the same tx is not fetched twice
- stop when one of the defined stop conditions is reached
- do not infer `illegal` from temporary provider failure
- unresolved dependency remains unresolved until local evidence supports a stronger verdict

## Required stop conditions

- `trusted_root_reached`
- `valid_issue_reached`
- `illegal_root_found`
- `already_visited`
- `missing_dependency`
- `budget_exceeded`
- `provider_rate_limited`
- `provider_error`

## Required safety limits

- throttle: `10 req/sec`
- max fetches per repair: bounded, configurable
- max traversal depth: bounded, configurable

## Ownership zones

Primary zones:
- `repo-governance`
- `indexer-ingest-orchestration`
- `external-chain-adapters`
- `verification-and-conformance`

Supporting zones:
- `indexer-state-and-storage`
- `service-bootstrap-and-ops`

## Wave ledger

| slice | zone lead | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `RL1` Contract Freeze | `repo-governance` | `operator/governance` | `todo` | - | docs review | reverse-lineage policy, limits, and stop conditions are frozen |
| `RL2` JungleBus Throttled Fetcher | `external-chain-adapters` | `operator/integration` | `todo` | `RL1` | focused tests | JungleBus validation fetch path is rate-limited to 10 req/sec |
| `RL3` Reverse Traversal Engine | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `RL1`,`RL2` | runtime tests | validation repair walks ancestry backward with visited/budget rules |
| `RL4` Repair Integration | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `RL3` | integration tests | validation repair worker uses reverse-lineage acquisition for tx-level repair |
| `RL5` Ops Visibility | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `RL4` | ops proof | stop reasons / budget exhaustion / rate-limit outcomes are observable |
| `A1` Closeout Audit | `repo-governance` | `operator/governance` | `todo` | `RL5` | audit | reverse-lineage validation fetch is real and honestly bounded |

## Definition of Done

- validation repair uses reverse-lineage fetch for tx-level lineage repair
- JungleBus `getRawTx` is throttled to `10 req/sec`
- traversal is bounded by visited/depth/budget rules
- provider failures do not incorrectly become illegal-root verdicts
- operator can tell whether repair stopped due to root success, budget, or provider limits
