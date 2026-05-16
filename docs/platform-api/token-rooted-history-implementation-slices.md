# Token Rooted History Implementation Slices

## Purpose

This document turns rooted token history into bounded implementation slices.

It replaces the generic idea of `historical_token_scan` with a security-first model:
- token `full_history` requires explicit trusted roots
- unknown-root branches do not auto-expand
- canonical token history exists only inside the trusted-root universe

## Slice Table

| slice | zone | owner | depends_on | validation | done_when |
|---|---|---|---|---|---|
| `TT01` | `repo-governance` | `operator/governance` | - | docs review | rooted token history task package and evidence paths exist |
| `TT02` | `public-api-and-realtime` | `operator/api` | `TT01` | API contract review | token registration and upgrade contract require `tokenHistoryPolicy.trustedRoots[]` for `full_history` |
| `TT03` | `indexer-state-and-storage` | `operator/state` | `TT01` | state-model tests | tracked token docs and status docs persist trusted roots, rooted security state, and unknown-root findings |
| `TT04` | `public-api-and-realtime` | `operator/api` | `TT02`,`TT03` | controller tests | token registration and full-history upgrade reject requests without trusted roots |
| `TT05` | `indexer-ingest-orchestration` | `operator/runtime` | `TT03` | planner tests | rooted token backfill planner creates work only from trusted roots |
| `TT06` | `indexer-ingest-orchestration` | `operator/runtime` | `TT05` | orchestration tests | unknown-root encounters are recorded as findings and do not create downstream canonical expansion work |
| `TT07` | `external-chain-adapters` | `operator/integration` | `TT05` | fixture replay or adapter tests | external historical token fetches can support rooted lineage and branch hydration without assuming provider truth |
| `TT08` | `indexer-ingest-orchestration` | `operator/runtime` | `TT06`,`TT07` | integration tests | rooted token backfill worker emits normal observation facts into the journal only for trusted-root branches |
| `TT09` | `indexer-state-and-storage` | `operator/state` | `TT03`,`TT08` | projection/query tests | canonical token state and token history ignore unknown-root branches |
| `TT10` | `public-api-and-realtime` | `operator/api` | `TT04`,`TT09` | controller tests | token history/readiness responses expose rooted status and unknown-root findings clearly |
| `TT11` | `verification-and-conformance` | `operator/verification` | `TT04`,`TT08`,`TT09`,`TT10` | rooted token test wave | positive and negative rooted-history scenarios are covered end to end |
| `A1` | `repo-governance` | `operator/governance` | `TT11` | audit note | rooted token history is audited for semantic honesty, reuse, and AI-first seams |

## Execution Notes

### `TT02` Contract changes

Add token-specific control-plane policy:
- `tokenHistoryPolicy.trustedRoots[]`

Do not make trusted roots optional when `historyPolicy.mode = full_history`.

### `TT03` Rooted token state model

Persist at minimum:
- trusted roots
- trusted root counts
- completed trusted root counts
- unknown-root finding counts
- rooted security status
- blocking unknown-root marker

### `TT05` Rooted planner

Planner responsibilities:
- start only from explicit trusted roots
- distinguish lineage work from trusted branch expansion
- keep unknown-root findings outside canonical work frontier

### `TT06` Unknown-root policy

Default runtime policy:
- `reject_branch`

This means:
- record the finding
- stop canonical branch expansion
- do not auto-promote the root

### `TT08` Journal integration

Rooted token historical work still emits the same canonical observation facts used by live ingest.

Do not build a token-history shadow ingestion model.

### `TT09` Canonical state rule

Unknown-root branches must not affect:
- token balances
- token UTXO sets
- token history pages
- authoritative token readiness completion

### `TT10` Public semantics

Token history status should explain rooted completion, including:
- trusted-root counts
- unknown-root findings
- whether rooted history is secure

### `TT11` Required verification scenarios

Minimum scenarios:
- token full-history registration rejected without trusted roots
- token full-history upgrade rejected without trusted roots
- rooted positive lineage walk from a trusted root
- unknown-root branch recorded but not expanded
- unknown-root branch does not pollute canonical token state
- token does not become `full_history_live` while blocking unknown-root frontier remains
- token becomes `full_history_live` after all trusted roots complete and no blocking unknown-root frontier remains
- replay and reorg preserve rooted security semantics
