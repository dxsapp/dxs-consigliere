# Program Audit A2 — Consigliere Thin-Node Observer Program
Reviewer: GPT-5 Codex
Date: 2026-05-16
Verdict: APPROVE WITH CHANGES

## Executive summary

The revision materially addresses the A1 blockers: the journal contract, reorg semantics, watchlist validation, Wave 5 dependency, and production-ops scope are now explicit instead of implicit. I would not send this back as another major revision, but it is not clean enough to open Wave 1 unchanged because the revised package introduces several execution-order and zone-mapping defects that can misroute child waves.

## A1 finding closure table

| A1 finding | Status | Where addressed | Residual / verification note |
|---|---|---|---|
| C1 — journal contract not source-agnostic | closed | `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/consigliere-thin-node-observer-program/master.md:69-75`; `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/consigliere-thin-node-observer-program/slices.md:82-92` | Correctly recast as Wave 2's mandatory first slice. Source check confirms current code still only has `AppendAsync(TxMessage)` and no `P2p` source (`TxObservationJournalWriter.cs:21-35`, `TxObservation.cs:12-17`), so the prerequisite is real. |
| C2 — Wave 5 falsely independent | closed | `master.md:211-220`, `master.md:265`, `slices.md:226-234` | W5 now depends on W2 and W4. W5/W6 ordering remains ambiguous; treated as a new finding below. |
| H1 — shared file risk | partial | `master.md:100-106`, `master.md:252-255`, `slices.md:37-53` | The contract-freeze slice is the right mechanism, but the surface is inconsistent and likely incomplete for later ops telemetry. |
| H2 — reorg projection semantics | closed | `master.md:76-81`, `master.md:180-194`, `slices.md:151-156` | Verified against source: `TxLifecycleProjectionRebuilder` handles disconnected block observations by setting `LifecycleStatus = Reorged`, clearing block hash/height, and setting `SeenInMempool = null` (`TxLifecycleProjectionRebuilder.cs:198-227`). |
| H3 — watchlist reuse | closed | `master.md:82-89`, `slices.md:94-108`, `slices.md:133-141` | Revision acknowledges `WatchingAddress` has only `Name`/`Address` (`WatchingAddress.cs:3-8`) and makes correctness/scale a Wave 2 slice. |
| H4 — reorg validation thin | closed | `master.md:191-194`, `slices.md:178-191` | Adds 1/2/N-depth tests, beyond-window degraded state, provider mismatch, idempotency, and replay validation. |
| H5 — production-ops missing | closed | `master.md:225-236`, `slices.md:264-300` | Production ops is now Wave 6. Scope sizing is a new issue, not an A1 closure failure. |
| M1 — repo zone mapping | partial | `master.md:113-143` | A mapping table exists, but several claimed repo zones are not in the catalog (`indexer-write-path`, `admin-ui`, `tests`). |
| M2 — Raven coupling | closed | `master.md:69-75`, `slices.md:82-92`, `launch-prompt.md:38-41` | The program forces new observer code through the journal contract rather than a parallel Raven store. Storage remains RavenDB by design. |
| M3 — fuzzy validation | partial | `master.md:261-266`, `slices.md:61-67`, `slices.md:211-216`, `slices.md:338-354` | Most fuzzy language is replaced with thresholds and exact fixture assertions. Watchlist latency thresholds drift and need a harness definition. |
| L1 — ledger columns | closed | `master.md:257-266` | Ledger now includes zone lead, depends_on, validation, done_when, and audit. |
| L2 — mission contradiction | closed | `launch-prompt.md:5-11`, `launch-prompt.md:27-37` | Mission now explicitly carves out the versioned `Broadcast` contract change. |

## New findings from the revision

### HIGH — N1. Repository-zone mapping still uses non-existent repo zones

**Where:** `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/consigliere-thin-node-observer-program/master.md:120-143`; `/Users/imighty/Code/dxs-consigliere/docs/repository-zones/zone-catalog.md:5-14`

**Evidence:** The revision maps `consigliere-broadcast` to `indexer-write-path`, `admin-ui` to `admin-ui`, and test zones to `tests` (`master.md:131`, `master.md:135-137`). Those are not repo-zone names in the catalog. The catalog names are `public-api-and-realtime`, `verification-and-conformance`, `repo-governance`, `service-bootstrap-and-ops`, and the other listed rows (`zone-catalog.md:5-14`). `BroadcastService.cs` is explicitly owned by `public-api-and-realtime` (`zone-catalog.md:11`), and `tests/**` is `verification-and-conformance` (`zone-catalog.md:13`).

**Why it matters:** A child wave using the program table as routing truth will miss required handoffs. W5 is especially exposed: broadcast unification is not an `indexer-write-path` change per the current catalog; it crosses `public-api-and-realtime`, `external-chain-adapters`, and likely `indexer-ingest-orchestration` because `BitcoindService.cs` is listed under that zone (`zone-catalog.md:10`).

**Recommendation:** Before Wave 1 opens, replace non-catalog names with catalog names or explicitly revise `zone-catalog.md` in a separate governance change. Map `src/admin-ui/**` to an existing or newly cataloged zone; map tests to `verification-and-conformance`; map `docs/platform-api/**` runbook/change notes to `repo-governance`.

### HIGH — N2. Wave 1 contract-freeze surface is inconsistent and incomplete for W6 telemetry

**Where:** `master.md:100-106`; `slices.md:37-53`; `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/P2p/Session/PeerSession.cs:191-215`, `PeerSession.cs:219-263`, `PeerSession.cs:284-304`

**Evidence:** `master.md` says Wave 1 pre-declares `OnHeadersReceived`, `OnBlockInvReceived`, and `OnInvReceived(tx)` (`master.md:100-104`). `slices.md` adds `OnRejectReceived` and broadens `OnInvReceived` to `InvMessage` (`slices.md:39-42`). Current `PeerSession` has only `IncomingMessages`, typed send helpers, and `OnAddrReceived` (`PeerSession.cs:61`, `PeerSession.cs:191-215`). W6 adds peer scoring and alerts (`slices.md:264-300`) but the frozen surface does not include send/receive telemetry, disconnect reason events, ping/pong latency, getdata/tx relay timing, or per-peer reject accounting. Those signals live in the receive/send loops (`PeerSession.cs:219-263`, `PeerSession.cs:284-304`) and are exactly the data peer scoring normally needs.

**Why it matters:** The package claims later waves will not add new hub or `PeerSession` extension points silently (`master.md:252-255`), but W6 is likely to need new session telemetry unless peer scoring is reduced to coarse `Completion` and timeout data. That reintroduces the shared-file risk A1 flagged.

**Recommendation:** Freeze either a generic session telemetry interface/event stream in Wave 1 or explicitly list the W6 callbacks and metrics fields. Also reconcile `OnInvReceived(tx)` vs `OnInvReceived(InvMessage)` and include `OnRejectReceived` consistently in `master.md`.

### MEDIUM — N3. Wave 6 is too broad unless it is internally split, and the W5/W6 order is not committed in all files

**Where:** `master.md:225-236`, `master.md:249-251`, `slices.md:264-300`, `slices.md:333-336`, `launch-prompt.md:70-79`

**Evidence:** Wave 6 contains peer scoring/rotation, alert poller, inbound listener integration, admin endpoints, admin UI, runbook, soak documentation, and public API change notes (`master.md:225-236`, `slices.md:271-300`). The dependency text says W5 must close before W6 or W6 can run before W5, default W5 first (`master.md:249-251`); `slices.md` repeats the alternative (`slices.md:333-336`); `launch-prompt.md` says strict sequencing W1 through W6 and places W6 after W5 (`launch-prompt.md:70-79`).

**Why it matters:** Some W6 work is safety before W5, such as peer scoring and pool-size alerts before removing HTTP broadcast fallback. Other W6 work is after W5, such as public `Broadcast` change notes. Keeping both in one wave makes the dependency graph describe two incompatible intentions.

**Recommendation:** Commit to W5 -> W6 everywhere, or split W6 into W6a production safety before W5 and W6b docs/change notes after W5. If kept as one wave, state that the W5 dependency is operator preference, not a hard technical dependency.

### MEDIUM — N4. Bounded validation thresholds are better, but not all are measurable as written

**Where:** `slices.md:61-67`, `slices.md:106`, `slices.md:139-141`, `slices.md:338-345`

**Evidence:** W1 requires every WhatsOnChain mainnet block over 24h to produce `OnNewBlock` with p95 lag <= 2s (`slices.md:61-67`). That is measurable if the wave builds a timestamped event collector and defines the explorer polling cadence; the package does not state that harness. W2 requires <= 50 ns hot path in the mandatory slice (`slices.md:106`) but the program validation matrix says lookup p99 <= 100 ns (`slices.md:344`). In .NET, <= 50 ns for real parsing-adjacent matching is only meaningful in an isolated BenchmarkDotNet microbenchmark, not an integration fixture with 10K transactions (`slices.md:139-141`).

**Why it matters:** Thresholds that cannot be reproduced become audit arguments instead of acceptance evidence. The numbers are not obviously wrong, but the test layer is underspecified.

**Recommendation:** Define the benchmark harness, hardware/runtime assumptions, and whether the latency target is median, p95, p99, or single-operation mean. Reconcile 50 ns vs 100 ns before Wave 2 opens.

### LOW — N5. Prerequisite slices are described, but child-wave gating must make them enforceable

**Where:** `slices.md:19-24`, `slices.md:82-93`, `launch-prompt.md:86-96`

**Evidence:** The package says Wave 1 opens with a contract-freeze slice and Wave 2 opens with journal-contract extension (`slices.md:19-24`, `slices.md:82-93`). The actual child wave packages do not exist yet, and the launch prompt only gates between waves, not between prerequisite and main slices (`launch-prompt.md:86-96`).

**Why it matters:** An execution operator could spawn main Wave 1 or Wave 2 implementation work in parallel with the prerequisite slice unless the child package ledger encodes `depends_on`.

**Recommendation:** When opening Wave 1 and Wave 2 packages, make all main slices depend on the prerequisite slice and add a stop/audit point after that slice closes.

## What the revision got right

The A1 core integration issues were not hand-waved. The revision correctly states that `TxObservationJournalWriter` is not source-neutral today and makes the source-neutral append plus `TxObservationSource.P2p` a mandatory first slice (`slices.md:82-92`). It also keeps the existing projection as the canonical observed-tx view and correctly describes current reorg behavior; the source code verifies the `Reorged` transition on disconnected block observations.

The watchlist section is much stronger. It no longer assumes a precomputed hash field that does not exist, and it adds address-output, address-input, token-output, deletion, collision, and 500K-address scale validation (`slices.md:94-108`). The open-question answer also aligns with the program target of <=500K addresses and a 1M soft upper test (`master.md:287-292`).

The production-ops gap is now visible. Adding Wave 6 is the right response to A1 H5, and the chosen contents are directionally relevant: peer scoring, alerts, inbound listener posture, runbook, and change notes are the right categories for an operator-grade thin-node engine.

## Verdict rationale

This is no longer a major-revision package. The previous critical findings are either closed or converted into explicit prerequisite work, and the revised Wave 2 and Wave 3 scopes now match the current code surfaces instead of pretending the surfaces already exist. The source verification supports the revised reorg claim.

The remaining problems are mostly consistency and execution-safety issues in the revised docs. They are still important: invalid zone names undermine the repository routing rules, and the contract-freeze promise is not yet strong enough to prevent later `PeerSession` churn. Those are small edits relative to the A1 rewrite, but they should be fixed before opening child packages.

## Recommendation

- approve with blocking doc changes before Wave 1 package opening:
- fix the repo-zone mapping to use only catalog zones, or update the catalog separately
- reconcile the Wave 1 contract-freeze enumeration and include W6 session telemetry needs
- commit to W5/W6 ordering, or split W6 around the broadcast-removal safety boundary
- define the W1/W2 benchmark harness and reconcile the 50 ns vs 100 ns watchlist target
- encode prerequisite-slice `depends_on` gates in the Wave 1 and Wave 2 child packages when they are created
