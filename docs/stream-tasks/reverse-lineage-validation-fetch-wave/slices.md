# Reverse Lineage Validation Fetch Wave Slices

## RL1 Contract Freeze

### Goal
Freeze the validation-fetch reverse-lineage contract.

### Must define
- scope: single-tx lineage repair only
- traversal direction: backward only
- rate limit: `10 req/sec`
- stop reasons
- traversal budgets

### Done when
- implementation can follow one explicit policy without re-deciding semantics

## RL2 JungleBus Throttled Fetcher

### Goal
Add a dedicated throttled JungleBus `getRawTx` path for validation-fetch work.

### Requirements
- bounded rate limiter at `10 req/sec`
- deterministic retry/backoff semantics for transient provider failures
- no hidden unlimited fast path

### Done when
- provider adapter or fetch service enforces the throttle in validation-fetch mode

## RL3 Reverse Traversal Engine

### Goal
Walk unresolved lineage backward through parent tx dependencies.

### Requirements
- visited-set dedupe
- parent expansion only from lineage-relevant edges
- bounded depth/fetch count
- explicit stop reason on exit

### Done when
- one repair request can build a bounded ancestry walk without duplicate fetch churn

## RL4 Repair Integration

### Goal
Plug reverse-lineage traversal into the validation repair subsystem.

### Requirements
- tx-level unresolved repair uses reverse-lineage mode
- successful fetches still flow through local parse/store/update path
- targeted revalidation still runs after acquisition

### Done when
- validation repair no longer behaves like generic blind tx fetching for lineage work

## RL5 Ops Visibility

### Goal
Expose enough status for operators to understand why a repair stopped.

### Useful fields
- last stop reason
- fetched tx count
- budget used
- rate-limit triggered
- provider error summary

### Done when
- operators can distinguish success, blocked lineage, and bounded-stop outcomes

## A1 Closeout Audit

### Goal
Close only when the wave is implemented as a bounded lineage strategy, not just copy.

### Must verify
- throttle works
- traversal is bounded
- stop reasons are honest
- residuals are documented
