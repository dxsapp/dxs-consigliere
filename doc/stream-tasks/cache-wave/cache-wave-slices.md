# Cache Wave Slice Plan

## Scope

This wave adds event-invalidated read caching to `Consigliere` hot projection-backed read surfaces.

Primary targets:
- address history
- address balances
- address UTXO set
- token history
- token balances
- token UTXO set

Foundational constraints:
- invalidation-first, not TTL-first
- projection-driven invalidation only
- backend-agnostic contract
- correctness under replay and reorg
- no write-path caching
- no primary-state or journal caching

## Slice Table

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| C01 | repo-governance | operator/governance | todo | - | docs review | durable cache-wave package and evidence paths exist |
| C02 | platform-common | operator/platform | todo | C01 | build + unit tests | `IProjectionReadCache` and narrow cache contracts exist without backend leakage |
| C03 | indexer-state-and-storage | operator/state | todo | C02 | contract/unit tests | canonical cache key space exists for address/token read shapes |
| C04 | platform-common | operator/platform | todo | C02 | unit tests + allocation sanity | bounded in-process cache backend exists and respects contract semantics |
| C05 | indexer-state-and-storage | operator/state | todo | C03,C04 | service tests | `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/AddressHistoryService.cs` is cache-backed |
| C06 | indexer-state-and-storage | operator/state | todo | C03,C04 | service tests | `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/UtxoSetManager.cs` balances and UTXO reads are cache-backed |
| C07 | indexer-state-and-storage | operator/state | todo | C03,C04 | query/service tests | token history/balance/UTXO reads are cache-backed |
| C08 | indexer-state-and-storage | operator/state | todo | C05,C06,C07 | integration tests | address/token projection rebuilders publish precise invalidation events on apply/revert |
| C09 | service-bootstrap-and-ops | operator/platform | todo | C04,C08 | startup/config tests | cache wiring and config shape land cleanly in startup/DI |
| C10 | verification-and-conformance | operator/verification | todo | C05,C06,C07,C08,C09 | integration tests | replay/reorg-safe correctness suite exists for memory backend |
| A1 | repo-governance | operator/governance | todo | C10 | audit note | first cache-wave audit gate passed or remediation slices opened |
| C11 | verification-and-conformance | operator/verification | todo | A1 | benchmark suite | baseline cache benchmarks exist for memory backend |
| C12 | platform-common | operator/platform | todo | A1 | build + isolated tests | `AzosPileProjectionReadCache` spike exists behind the same abstraction |
| C13 | verification-and-conformance | operator/verification | todo | C11,C12 | comparative benchmarks | comparative memory vs `Azos` evidence exists for hot read shapes |
| A2 | repo-governance | operator/governance | todo | C13 | audit note | explicit `Azos` adoption decision is recorded |
| C14 | repo-governance | operator/governance | todo | A2 | docs review | closeout docs, benchmark notes, and recommendation are recorded |

## Detailed Slice Notes

### C01 - Durable Task Package

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/**`

Deliverables:
- `master.md`
- `cache-wave-slices.md`
- `cache-wave-launch-prompt.md`
- evidence directory layout

### C02 - Cache Contract

Owned zone:
- `platform-common`

Preferred owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Common/**`

Expected additions:
- `ProjectionCacheKey`
- `ProjectionCacheEntryOptions`
- `IProjectionReadCache`
- `IProjectionCacheKeyFactory`
- `IProjectionCacheInvalidationSink`
- narrow metrics hooks if needed

Rules:
- no `Azos` references here
- no `MemoryCache` references in public contracts
- values are typed at the call edge, backend remains opaque

### C03 - Canonical Key Space

Owned zone:
- `indexer-state-and-storage`

Preferred owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/**`

Required key families:
- address history
- address balances
- address UTXO set
- token history
- token balances
- token UTXO set

Rules:
- sort arrays before key materialization
- encode null filters explicitly
- include pagination in paged read keys
- do not allow duplicate hand-built key strings across services

### C04 - In-Process Backend

Owned zone:
- `platform-common`

Expected behavior:
- bounded
- safe under concurrent reads/writes
- optional tiny TTL only as a safety fuse, never as correctness model
- efficient invalidation by exact key

Explicitly out of scope:
- distributed cache
- persistent cache
- `Azos`

### C05 - Address History Integration

Owned zone:
- `indexer-state-and-storage`

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/AddressHistoryService.cs`

Acceptance focus:
- cached BSV history reads
- cached token-filtered history reads
- pagination correctness
- no stale reads after projection invalidation

### C06 - Address Balance and UTXO Integration

Owned zone:
- `indexer-state-and-storage`

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/UtxoSetManager.cs`

Acceptance focus:
- multi-address balances
- address-scoped UTXO reads
- token-filtered UTXO reads
- exact invalidation after address projection mutation

### C07 - Token Read Integration

Owned zone:
- `indexer-state-and-storage`

Preferred owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/**`
- relevant service consumers in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/**`

Acceptance focus:
- token history reads
- token balances reads
- token UTXO reads
- exact invalidation after token projection mutation

### C08 - Projection-Driven Invalidation

Owned zone:
- `indexer-state-and-storage`

Primary owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionRebuilder.cs`

Required behavior:
- invalidation published only when projection mutations are applied or reverted
- localized address/token invalidation
- no global reset shortcut

### C09 - DI and Config

Owned zone:
- `service-bootstrap-and-ops`

Preferred owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Startup.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Configs/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/**`

Config direction:
- `Consigliere:Cache`
- `Enabled`
- `Backend`
- bounded memory controls
- `Azos` child section reserved for later slice, not required to be enabled by default

### C10 - Correctness Suite

Owned zone:
- `verification-and-conformance`

Required coverage:
- hit/miss semantics
- invalidation on apply
- invalidation on revert
- replay safety
- reorg safety
- selective invalidation for address vs token scopes
- no stale reads after mutation waves

### A1 - Audit Gate

Audit questions:
- Did cache ownership stay out of controllers?
- Did invalidation stay at projection-apply/revert level?
- Is key building centralized enough for AI-first edits?
- Is the backend abstraction still clean enough for `Azos` to plug in without cross-zone churn?
- Are any remediations required before benchmark and `Azos` spike work?

### C11 - Memory Backend Benchmarks

Required benchmark families:
- address history hot read
- address balances hot read
- address UTXO hot read
- token history hot read
- invalidation churn
- replay/reorg churn effect on cache stability

### C12 - Azos Spike

Owned zone:
- `platform-common`

Expected integration boundary:
- behind `IProjectionReadCache` only
- no behavior changes in query services

Allowed reference repo:
- `/Users/imighty/Code/azos`

Decision rule:
- this slice exists to measure, not to force adoption

### C13 - Comparative Benchmarks

Compare:
- memory backend
- `Azos` backend

Required metrics:
- latency
- allocations
- memory footprint
- invalidation overhead
- churn under repeated localized updates

### A2 - Azos Decision Gate

Possible outcomes:
- reject
- keep optional
- recommend for large deployments

Default assumption unless evidence strongly shifts it:
- keep optional

### C14 - Closeout

Closeout outputs:
- benchmark evidence summary
- explicit backend recommendation
- follow-up risks
- excellent-state backlog additions if any remain

## Dependency Order

Critical path:
- `C01 -> C02 -> C03 -> C04 -> C05/C06/C07 -> C08 -> C09 -> C10 -> A1 -> C11/C12 -> C13 -> A2 -> C14`

Parallelizable windows:
- `C05`, `C06`, `C07` can run in parallel after `C03` and `C04`
- `C11` and `C12` can run in parallel after `A1`

## Benchmark Evidence Files

Expected files:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/benchmarks/C11-memory-backend-benchmarks.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/benchmarks/C13-comparative-memory-vs-azos.md`

## Audit Files

Expected files:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A2.md`

## Remediation Files

Naming convention:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/remediation/R-<gate>-<nn>.md`
