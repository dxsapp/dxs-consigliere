# Validation Dependency Repair Wave

## Goal

Turn `validation_fetch` into a real subsystem for:
- acquiring lineage-critical dependencies
- repairing unresolved `(D)STAS` lineage state
- triggering targeted revalidation
- exposing durable repair state to operators

`Consigliere` remains the authoritative local `(D)STAS` validation engine.
Providers remain dependency/data sources only.

## Why This Wave Exists

Today `validation_fetch` is preserved and described correctly, but it is still mostly a semantic/runtime contract.

The system can already say:
- validation is `unknown`
- `B2GResolved = false`
- dependencies are missing

But it does not yet provide one broad, durable subsystem that:
- plans validation-repair work
- deduplicates repeated repair requests
- fetches missing dependencies
- retries sanely
- triggers targeted revalidation
- exposes unresolved/stuck repair state in ops

## Canonical Model

### Validation authority

`Consigliere` computes the authoritative `(D)STAS` legality/rooted verdict locally.

### Provider role

Providers may supply:
- raw transactions
- dependency data
- block progression
- realtime observations

Providers do not supply:
- authoritative token validation verdicts
- trusted-root decisions
- canonical rooted-history truth

### Capability distinction

#### `raw_tx_fetch`
Fetch raw tx hex by txid.

#### `validation_fetch`
Acquire dependency data required for local authoritative lineage/B2G validation.

#### `validation_repair`
Durable asynchronous work that resolves unresolved lineage state and re-triggers local validation.

## Required Scenarios

The wave must explicitly cover:

1. public validate endpoint sees unresolved lineage
2. rooted-history expansion sees missing ancestry
3. token projection sees unknown root because dependencies are missing
4. late-arriving dependency triggers targeted revalidation
5. block/backfill progression exposes now-fetchable lineage gaps

If any scenario is not covered in v1, it must be documented honestly in closeout.

## Ownership Zones

Primary zones:
- `repo-governance`
- `indexer-state-and-storage`
- `indexer-ingest-orchestration`
- `public-api-and-realtime`
- `service-bootstrap-and-ops`
- `verification-and-conformance`

Supporting zone:
- `external-chain-adapters`

## Wave Ledger

| slice | zone lead | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `VR1` Contract Freeze | `repo-governance` | `operator/governance` | `done` | - | docs review | validation dependency contract, terminology, and scenario coverage are frozen |
| `VR2` Durable Repair Work | `indexer-state-and-storage` | `operator/state` | `done` | `VR1` | store tests | repair work items, states, and dedupe semantics are persisted |
| `VR3` Repair Worker | `indexer-ingest-orchestration` | `operator/runtime` | `done` | `VR1`,`VR2` | runtime tests | scheduler/worker fetches dependencies and retries sanely |
| `VR4` Targeted Revalidation | `indexer-state-and-storage` | `operator/state` | `done` | `VR2`,`VR3` | projection tests | acquired dependencies trigger affected tx/token revalidation |
| `VR5` API Integration | `public-api-and-realtime` | `operator/api` | `done` | `VR3`,`VR4` | API tests | public validation flow integrates with repair subsystem without changing local authority model |
| `VR6` Ops Surface | `service-bootstrap-and-ops` | `operator/platform` | `done` | `VR3`,`VR4` | ops tests + UI proof | unresolved/running/failed validation repair state is visible to operators |
| `A1` Closeout Audit | `repo-governance` | `operator/governance` | `done` | `VR6` | audit | `validation_fetch` is a real subsystem rather than only a semantic contract |

## Hard Boundaries

- do not turn providers into validation authorities
- do not collapse `raw_tx_fetch` and `validation_fetch`
- do not replace rooted-history semantics with generic explorer discovery
- do not build a generic workflow engine
- do not change public validation authority away from local `Consigliere` evaluation

## Definition Of Done

- unresolved lineage state becomes durable repair work
- repair execution acquires missing dependencies through a clear validation-dependency contract
- successful acquisition triggers targeted revalidation
- unresolved/stuck/failed repair state is operator-visible
- public validation still returns local authoritative verdicts
- docs, runtime, API, and ops surfaces all use one consistent mental model
