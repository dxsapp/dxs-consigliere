# History Sync Implementation Slices

## Purpose

This document turns the tracked history sync model into bounded implementation slices.

## Slice Table

| slice | zone | owner | depends_on | validation | done_when |
|---|---|---|---|---|---|
| `H01` | `repo-governance` | `operator/governance` | - | docs review | durable task package and evidence paths exist |
| `H02` | `public-api-and-realtime` | `operator/api` | `H01` | API contract tests | control-plane DTOs include `historyPolicy` and public history status shapes |
| `H03` | `indexer-state-and-storage` | `operator/state` | `H01` | state-model tests | tracked state/readiness docs store `historyReadiness`, `historyCoverage`, and history-policy fields |
| `H04` | `indexer-state-and-storage` | `operator/state` | `H03` | state-model tests | internal history backfill job/status documents exist with shared base and typed payloads |
| `H05` | `service-bootstrap-and-ops` | `operator/platform` | `H02`,`H03`,`H04` | startup/build tests | config, DI, and runtime registration support historical capability routing and backfill workers |
| `H06` | `indexer-ingest-orchestration` | `operator/runtime` | `H03`,`H05` | orchestration tests | `forward_only` boundary initialization records anchor, attaches realtime, closes gaps, and promotes `historyReadiness` to `forward_live` |
| `H07` | `indexer-ingest-orchestration` | `operator/runtime` | `H04`,`H05` | orchestration tests | explicit full-history backfill jobs queue, run, retry, complete, and fail with checkpointed progress |
| `H08` | `external-chain-adapters` | `operator/integration` | `H05` | adapter tests or fixture replay | explicit provider capability model supports `historical_address_scan` and `historical_token_scan` |
| `H09` | `indexer-ingest-orchestration` | `operator/runtime` | `H07`,`H08` | integration replay tests | historical scans emit normal observation facts into the canonical journal rather than a shadow history path |
| `H10` | `public-api-and-realtime` | `operator/api` | `H02`,`H06`,`H07` | controller tests | single-item and bulk full-history upgrade endpoints are idempotent and return public history status |
| `H11` | `indexer-state-and-storage` | `operator/state` | `H03`,`H06`,`H07`,`H09` | query tests | history query path enforces `acceptPartialHistory` and returns honest coverage metadata |
| `H12` | `verification-and-conformance` | `operator/verification` | `H06`,`H07`,`H09`,`H10`,`H11` | replay, readiness, and API tests | forward-only, full-history, upgrade, retry, degraded, and partial-history semantics are covered end to end |
| `A1` | `repo-governance` | `operator/governance` | `H12` | audit note | quality, reuse, AI-first seams, and semantic honesty are audited before further history expansions |

## Execution Notes

### `H06` `forward_only` boundary initialization

Implement:
- registration anchor recording
- realtime attach
- gap closure from anchor
- promotion to `forward_live`

This is not a full historical backfill job.

### `H07` Full-history backfill orchestration

Implement:
- queued/running/retry/completed/failed execution flow
- checkpoint persistence
- progress timestamps and counters

### `H08` Historical capability routing

Add explicit historical routing for:
- `historical_address_scan`
- `historical_token_scan`

### `H09` Journal integration

Historical sync must write the same journal observation facts used by live ingest.

Do not build a special history-only projection path.

### `H10` Upgrade endpoints

Add:
- single address full-history upgrade
- single token full-history upgrade
- bulk address full-history upgrade
- bulk token full-history upgrade

Make all operations:
- idempotent
- per-item in bulk mode

### `H11` History query enforcement

Enforce:
- deny by default when authoritative history is unavailable
- allow partial answers only when `acceptPartialHistory = true`
- allow partial only for `forward_live` and `backfilling_full_history`

### `H12` Verification

Required scenarios:
- `forward_only` tracked address
- `forward_only` tracked token
- registration with `full_history`
- upgrade `forward_only -> full_history`
- backfill retry
- terminal backfill failure to `degraded`
- `stateReadiness = live` while history is not full
- history denial without `acceptPartialHistory`
- partial history allowed with coverage metadata
