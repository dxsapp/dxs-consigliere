# Managed Scope Model

## Why Managed Scope Exists

`Consigliere` is designed to be a reliable and cost-efficient BSV backend without requiring every business to run its own full node.

That only works if indexing is selective.

The service does not promise full-network indexing. It promises high-quality state and realtime for explicitly managed scope.

## Scope Objects

### Tracked address
A BSV address explicitly registered for indexing, realtime, and derived state maintenance.

### Tracked token
A token identifier explicitly registered for indexing, realtime, and derived state maintenance.

### Derived scope
Transactions, UTXOs, balances, token state, and history that affect tracked addresses or tracked tokens.

### Assist lookup
A non-authoritative query outside tracked scope. These are allowed as helper operations but are not the core product promise.

## Tracking Modes

- `continuous`: maintain live state and realtime continuously
- `bootstrap_once`: backfill once for readiness or migration workflows
- `address_only`: address state without token-focused expansion
- `token_only`: token-focused state without arbitrary address expansion
- `address_and_token`: both dimensions maintained together

## Lifecycle States

- `registered`: accepted but not yet scheduled
- `backfilling`: historical state is being collected
- `catching_up`: historical coverage exists but live tip is not yet reached
- `live`: authoritative tracked state is available
- `degraded`: service still answers but guarantees are reduced
- `paused`: intentionally not advancing
- `failed`: tracking flow cannot continue without intervention

## Completeness Model

Completeness values exposed to clients:
- `authoritative`
- `catching_up`
- `best_effort`
- `not_tracked`

Rules:
- `authoritative` is allowed only when lifecycle is `live`
- `catching_up` is allowed during `backfilling` and `catching_up`
- `best_effort` is allowed during `degraded`
- `not_tracked` is for unregistered scope

## Authoritative State Rules

For a tracked entity in `live`:
- balances must reflect indexed ground truth
- UTXO queries must be consistent with internal indexed state
- token state and STAS/DSTAS classification must come from normalized internal rules
- realtime events must be aligned with the indexed state model

For a tracked entity not yet in `live`:
- the API may answer
- the response must expose lifecycle and lag
- the client must never infer full correctness from that response

## Scope Expansion Rules

The index should expand only as far as necessary to keep tracked scope authoritative.

Allowed expansion:
- transactions touching tracked addresses
- transactions touching tracked tokens
- parent/child dependencies required to derive correct current state
- temporary bootstrap state needed for backfill and conformance

Disallowed expansion as a default behavior:
- indiscriminate full-address graph crawling
- full-network token holder derivation
- explorer-style indexing of unrelated chain activity

## Readiness Signals

Each tracked entity should expose:
- lifecycle state
- latest indexed height
- lag in blocks
- lag in seconds when available
- degradation marker if upstream routing is operating below normal quality

## Realtime Contract

Realtime is defined for managed scope only.

The system should emit events when tracked scope changes state, including:
- relevant mempool observation
- confirmation
- reorg rollback
- drop/orphan handling
- balance changes
- scope catch-up completion
- degradation or recovery

## Product Invariants

1. Managed scope is the core unit of promise.
2. Authoritative state is only promised for managed scope in `live`.
3. Cost efficiency depends on keeping scope selective.
4. Public API must tell clients whether scope is tracked and whether it is ready.
5. Internal provider routing must never downgrade a response silently.
