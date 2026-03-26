# History Sync Model

## Purpose

This document defines how tracked address and tracked token history behaves in `Consigliere`.

It exists to keep history semantics honest inside a selective-indexed backend:
- no explorer-style promise
- no silent partial history
- no ambiguity about when current state is usable versus when history is authoritative

## Scope

This model applies only to tracked entities:
- `tracked_address`
- `tracked_token`

It does not introduce general untracked history reads.

## History Sync Modes

Only two history sync modes are allowed:
- `forward_only`
- `full_history`

Explicitly not supported:
- `last_n_blocks`

## Decision: `forward_only` Is the Recommended Default

`forward_only` is the recommended default for new tracked entities.

Meaning:
- the service does not promise historical completeness before the registration boundary
- it promises authoritative observation from the registration boundary forward once initialization is complete

## Decision: `full_history` Is Explicit and Expensive

`full_history` is allowed, but it is an explicitly expensive sync mode.

Operational note:
- a `full_history` sync may take a very long time
- long-running backfill is expected behavior, not a correctness failure

## Decision: `forward_only` Uses a Lightweight Boundary Initialization

`forward_only` does not become usable immediately at registration time.

It becomes usable only after a lightweight boundary initialization completes.

Minimum boundary requirements:
1. registration anchor recorded
2. realtime stream attached
3. gap closure from the anchor confirmed
4. tracked status updated only after the above steps complete

## Decision: `full_history` Attaches Realtime Immediately

`full_history` must not wait for full historical sync before attaching realtime ingestion.

Instead:
- realtime starts immediately
- historical backfill runs in the background

## Decision: Historical Sync Uses Explicit Capabilities

Historical sync uses explicit historical capabilities:
- `historical_address_scan`
- `historical_token_scan`

## Decision: Historical Sync Uses the Same Observation Journal

Historical sync must flow through the same observation journal used by live ingest.

Meaning:
- historical sources discover historical transactions
- those discoveries become normal observation facts in the journal
- projections and dedupe operate on the same canonical fact model as live ingest

## State Readiness and History Readiness

`stateReadiness` and `historyReadiness` are separate dimensions.

Normal combinations include:
- `stateReadiness = live`, `historyReadiness = not_requested`
- `stateReadiness = live`, `historyReadiness = forward_live`
- `stateReadiness = live`, `historyReadiness = backfilling_full_history`

Rule:
- `state live` does not imply `full history live`

## History Readiness Values

Minimum `historyReadiness` values:
- `not_requested`
- `forward_live`
- `backfilling_full_history`
- `full_history_live`
- `degraded`

## Decision: History Uses an Explicit Coverage Object

History authority is expressed through:
- `historyReadiness`
- `historyCoverage`

Minimum `historyCoverage` shape:
- `mode`
- `fullCoverage`
- `authoritativeFromBlockHeight?`
- `authoritativeFromObservedAt?`

## Query Semantics

### State Reads

State reads are gated by `stateReadiness`, not by `historyReadiness`.

Examples:
- balances
- UTXO sets
- token state
- current address state

### History Reads

History reads are gated by:
- `historyReadiness`
- `historyCoverage`
- explicit client acceptance of partial history

History endpoints must support:
- `acceptPartialHistory`

### Decision: Partial History Must Be Explicitly Accepted

`Consigliere` must never silently return partial history as if it were complete.

Partial history is allowed only when:
- `historyReadiness = forward_live`
- `historyReadiness = backfilling_full_history`

Partial history is not allowed when:
- `historyReadiness = not_requested`
- `historyReadiness = degraded`

### Decision: History Request Denial Is Preferred Over Silent Truncation

When a client requests authoritative history and that authority is not available:
- the request should be denied with readiness metadata
- the service must not return a misleading partial answer

## History Policy in Control Plane

Tracked entity registration uses a nested history policy object:
- `historyPolicy`
  - `mode`

Allowed `mode` values:
- `forward_only`
- `full_history`

## Decision: `full_history` Is Also an Upgrade Operation

`full_history` is supported in two ways:
1. at registration time
2. as an explicit upgrade from `forward_only`

Allowed upgrade:
- `forward_only -> full_history`

Explicitly not allowed:
- `full_history -> forward_only`

## Full-History Upgrade Semantics

On upgrade:
- current state remains usable if `stateReadiness = live`
- `historyReadiness` becomes `backfilling_full_history`
- a historical backfill job is created or resumed

The upgrade operation should be:
- idempotent
- available as single-item and bulk control-plane operations

## Internal Backfill Execution Model

Public readiness and internal execution state are separate.

Public:
- `historyReadiness`
- `historyCoverage`
- `backfillStatus?`

Internal:
- execution job state
- provider and capability
- cursor and counters

### Minimum Internal Backfill Execution Status

Minimum internal execution statuses:
- `queued`
- `running`
- `waiting_retry`
- `completed`
- `failed`

### Decision: `waiting_retry` Does Not Automatically Mean Public `degraded`

Transient retry/backoff is still active work.

It should not automatically flip public history status to `degraded`.

## Internal Backfill Status Shape

Use a shared base structure with typed payloads for address- and token-specific details.

Minimum shared base:
- `entityType`
- `entityId`
- `historyMode`
- `status`
- `requestedAt`
- `startedAt?`
- `lastProgressAt?`
- `completedAt?`
- `sourceCapability`
- `provider?`
- `cursor?`
- `itemsScanned`
- `itemsApplied`
- `lastObservedHistoricalBlockHeight?`
- `errorCode?`
- `attemptCount`

### Address-Specific Payload

Minimum fields:
- `anchorBlockHeight`
- `oldestCoveredBlockHeight?`
- `cursor?`
- `discoveredTransactionCount`

### Token-Specific Payload

Minimum fields:
- `anchorBlockHeight`
- `oldestCoveredBlockHeight?`
- `cursor?`
- `discoveredTransactionCount`
- `lineageBoundaryReached`
- `historyBoundaryReached`

## History Transition Matrix

### History Readiness Transitions

Allowed:
- `not_requested -> forward_live`
- `not_requested -> backfilling_full_history`
- `forward_live -> backfilling_full_history`
- `backfilling_full_history -> full_history_live`
- `backfilling_full_history -> degraded`
- `full_history_live -> degraded`
- `forward_live -> degraded`
- `degraded -> forward_live`
- `degraded -> backfilling_full_history`
- `degraded -> full_history_live`

Disallowed:
- `full_history_live -> forward_live`
- `full_history_live -> not_requested`
- `backfilling_full_history -> forward_live`

### Backfill Execution Status Transitions

Allowed:
- `queued -> running`
- `running -> waiting_retry`
- `waiting_retry -> running`
- `running -> completed`
- `running -> failed`
- `waiting_retry -> failed`

Disallowed:
- `completed -> running`
- `completed -> queued`

## Public History Status Object

Minimum public history object:
- `historyReadiness`
- `historyCoverage`
- `backfillStatus?`

Minimum public `backfillStatus` shape:
- `status`
- `requestedAt`
- `startedAt?`
- `lastProgressAt?`
- `completedAt?`
- `itemsScanned`
- `itemsApplied`
- `errorCode?`

## Invariants

1. No silent partial history.
2. `stateReadiness` and `historyReadiness` may diverge.
3. `forward_only` is authoritative only from its anchor.
4. `full_history` uses explicit historical capabilities.
5. Historical sync uses the same observation journal as live ingest.
6. `full_history` intent is monotonic: no downgrade back to `forward_only`.
