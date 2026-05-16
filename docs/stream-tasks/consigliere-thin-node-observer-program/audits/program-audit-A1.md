# Program Audit A1 — Consigliere Thin-Node Observer Program
Reviewer: GPT-5 Codex
Date: 2026-05-16
Verdict: MAJOR REVISION REQUIRED

## Executive summary

The program is directionally sensible, but the package is not execution-ready. It treats several integration contracts as clean reuse when the code shows they are not clean yet, especially the tx journal writer, source taxonomy, watchlist matching, and projection behavior under reorg. The wave ordering is mostly right for stop-and-audit delivery, but Wave 5 is not independent and the shared `WalletHub` / `PeerSession` edits need an explicit compatibility slice before child waves open. The missing production-ops scope is also too large to leave implicit after five waves that claim an operator-grade thin-node engine.

## Critical findings

### C1. The “existing journal spine” is not a clean source-agnostic append contract yet

**Where:** `master.md:Core Rules`; `slices.md:Program Overview`, `Wave 2`.

**Evidence:** The program says P2P "must append to the existing `TxObservationJournalWriter` with source tag `"p2p"`" and `SeenBySources` accumulates source tags (`master.md` lines 61-66). But `TxObservationJournalWriter.AppendAsync` accepts only `TxMessage`, not a source-neutral observation DTO (`TxObservationJournalWriter.cs` lines 21-35), and its `TryCreateObservation` is coupled to `TxMessage.Type` and `TxMessage.Transaction` (`TxObservationJournalWriter.cs` lines 71-105). The known source constants are only `"node"`, `"junglebus"`, and `"bitails"` (`TxObservation.cs` lines 12-17); there is no `"p2p"` source constant.

**Why it matters:** Wave 2 is not just “plug in another source.” It must first create or formalize a source-neutral append contract, add a `p2p` source identity, and decide how raw payload persistence works when P2P sees an `inv` before it has fetched full tx bytes. If this tax is discovered mid-wave, Wave 4 metrics and Wave 5 broadcast lifecycle will build on unstable semantics.

**Recommendation:** Add a prerequisite slice before Wave 2, or make it the first slice of Wave 2: define `TxObservationSource.P2p`, expose an append method that accepts `TxObservation` plus optional raw payload, and add tests proving `SeenBySources` includes `p2p` after projection rebuild.

### C2. Wave 5 is not independent and should not be represented as having no hard dependency

**Where:** `master.md:Cross-Wave Dependency Rules`; `slices.md:Dependency chain`, `Wave 5`.

**Evidence:** The package marks Wave 5 as independent (`slices.md` lines 168-173) and with `depends_on` empty (`master.md` line 175). But it touches `WalletHub` and `IWalletHub` (`slices.md` lines 146-148), which are also touched by Waves 1 and 3 (`slices.md` lines 33-35, 100-101). It also rewrites `IBroadcastService` / `BroadcastService` (`slices.md` lines 147-149), whose current shape contains both legacy provider broadcast and Gate 3 `SubmitAsync` (`IBroadcastService.cs` lines 7-20; `BroadcastService.cs` lines 45-87, 272-320). The program says the journal/projection pipeline is the integration spine for state transitions (`slices.md` lines 11-14), so broadcast unification needs the same source-observation semantics Wave 2 is supposed to stabilize.

**Why it matters:** If Wave 5 runs early, it breaks public API and broadcast semantics before the observer side can prove its state signals. If it runs last, the dependency is still real: it consumes W2 source attribution and shares W1/W3 hub files. Marking it independent invites a future operator to reorder it incorrectly.

**Recommendation:** Set Wave 5 `depends_on = W2` at minimum, and probably `W4` if source metrics are part of the operator acceptance of removing HTTP fallback. Keep it last unless a separate Wave 0 freezes the hub/broadcast contract first.

## High-severity findings

### H1. Shared-file risk is understated; “sequential hub events” is not enough

**Where:** `master.md:Ownership Zones`, `Cross-Wave Dependency Rules`; `slices.md:Waves 1, 3, 5`.

**Evidence:** `consigliere-hub-public` is listed as a shared zone (`master.md` lines 101-103), and the rule is only that hub event additions happen sequentially (`master.md` lines 163-165). Existing `WalletHub` already has legacy `Broadcast`, `BroadcastTracked`, and `SubscribeToBroadcast` in the same method region (`WalletHub.cs` lines 129-154), while `IWalletHub` already has `OnTransactionDeleted` and `OnBroadcastStateChanged` (`IWalletHub.cs` lines 7-25). Wave 1 adds `OnNewBlock`; Wave 3 adds `OnReorg`; Wave 5 changes `Broadcast`.

**Why it matters:** The merge risk is not just textual. It is API contract drift: event names, group naming, subscription authorization, and vnext compatibility can diverge wave by wave. The same problem exists in `PeerSession`: the current design exposes `IncomingMessages` plus only `OnAddrReceived` (`PeerSession.cs` lines 61, 210-215), but Waves 1 and 2 propose new callbacks into the same file (`slices.md` lines 28-29, 62-64).

**Recommendation:** Before Wave 1 implementation, create a small contract-freeze slice in the Wave 1 child package: declare all planned hub events/subscription groups and all `PeerSession` observation extension points. Prefer one generic typed inbound dispatcher over adding one callback per message type.

### H2. Reorg projection behavior is materially misdescribed

**Where:** `slices.md:Wave 3`; `master.md:Program Ledger`.

**Evidence:** Wave 3 says orphaned tx documents are reverted "to mempool state" with `SeenInMempool=true`, `BlockHash=null`, and `BlockHeight=null` (`slices.md` lines 89-94). Existing projection code does not do that for a confirmed tx. A mempool observation is ignored if the lifecycle is already `Confirmed` (`TxLifecycleProjectionRebuilder.cs` lines 161-163). A block disconnect currently moves matching txs to `Reorged`, sets `SeenInMempool = null`, and clears block fields (`TxLifecycleProjectionRebuilder.cs` lines 198-227).

**Why it matters:** Wave 3 depends on projection semantics that do not exist. This is not a small “tiny change flagged before slice work begins” (`slices.md` lines 102-103); it is the core state transition of the wave.

**Recommendation:** Make projection reorg groundwork explicit before Wave 3 opens. Define whether the canonical post-reorg state is `Reorged`, `SeenInMempool`, or a two-step `ReorgedPendingRescan -> SeenInMempool`, then add replay tests before hub events or provider rescans.

### H3. Watchlist reuse is shakier than the package claims

**Where:** `master.md:Core Rules`; `slices.md:Wave 2`.

**Evidence:** The program specifies `WatchingAddress.Hash160[0..8]` as `ulong` keys and Raven Subscription hot reload (`master.md` lines 67-69; `slices.md` lines 68-70). The actual `WatchingAddress` model stores only `Name` and `Address` (`WatchingAddress.cs` lines 3-8); `WatchingToken` stores `TokenId` and `Symbol` (`WatchingToken.cs` lines 3-8). Current matching happens through `TransactionFilterWatchSet`, which parses full transactions and checks output/input addresses and token ids (`TransactionFilterWatchSet.cs` lines 49-94). Bitails realtime builds provider topics from `TransactionStore.GetWatchingAddresses()` and falls back to all transactions when tokens exist (`BitailsRealtimeSubscriptionScopeProvider.cs` lines 15-39).

**Why it matters:** A P2P `inv` observer cannot cheaply match watchlist scope without fetching and parsing tx bytes, and tokens may force broad observation. The HashSet-prefix design is a new optimization with collision, false-positive, and hot-reload semantics that are not proven by existing code.

**Recommendation:** Add a Wave 2 validation slice for watchlist correctness and scale: address outputs, address inputs, STAS/DSTAS token outputs, token redeem address behavior, deletions/tombstones, and large-watchlist memory/load benchmarks.

### H4. Reorg validation is too thin for a program-level done gate

**Where:** `slices.md:Wave 3`, `Validation matrix`; `master.md:Scope`.

**Evidence:** The package caps the header window at `≤200` (`master.md` lines 53-54) but gives no evidence or operational response for deeper divergence. Wave 3 validation is a loopback peer fork test, with real-mainnet validation deferred until a rare genuine reorg (`slices.md` lines 109-113). Program validation only says "Reorg handling without tx duplication" via loopback (`slices.md` lines 207-212).

**Why it matters:** Loopback is necessary but not sufficient. The failure modes are projection replay, provider block-body mismatch, partial rescans, deep reorg beyond retained headers, and idempotency of repeated disconnect facts.

**Recommendation:** Expand Wave 3 completion criteria: deterministic 1-, 2-, and N-depth fork tests; a beyond-window test that enters an explicit degraded/manual-repair state; provider-content mismatch tests; and projection replay from journal from zero.

### H5. Production-ops scope is missing from a program that claims operator-grade end state

**Where:** `master.md:Goal`, `Definition of Done`; `slices.md:Wave 4`.

**Evidence:** The goal promises operator-grade observability and a healthy admin panel (`master.md` lines 19-22, 188-191). The earlier design explicitly deferred inbound listener, peer scoring/rotation, OpenTelemetry, admin UI pages, operator alerts, and extmsg lifting to Gate 4 (`consigliere-thin-node-design.md` lines 754-762). This program includes admin source metrics, but no wave for peer scoring, inbound listener integration, pool alerts, relay-back alerts, reorg-depth alerts, or public API documentation. `tools/BsvBroadcastNode` has a rough inbound listener (`PeerNodeHost.cs` lines 97-172), but it is a dev tool and not production integrated.

**Why it matters:** A bidirectional thin-node engine without pool health alerts and peer rotation is not production-operable. The operator will discover failure only through missing business events.

**Recommendation:** Add Wave 6 for production operations, or explicitly downgrade the program goal to “observer MVP.” Include peer scoring/rotation, alert thresholds, inbound listener decision, runbook, and API/operator docs.

## Medium-severity findings

### M1. Program ownership zones do not map cleanly to repository zones

**Where:** `master.md:Ownership Zones`.

**Evidence:** The package invents zones such as `bsv-p2p-session`, `consigliere-p2p-services`, and `consigliere-hub-public` (`master.md` lines 92-107). The repository zone catalog uses broader accountable zones and requires handoff between state, orchestration, realtime API, and ops paths (`zone-catalog.md` lines 5-14, 18-20).

**Recommendation:** Add a mapping table from program zones to repository zones and list required handoffs per wave. This matters especially for W2/W3 because they cross `indexer-state-and-storage`, `indexer-ingest-orchestration`, and `public-api-and-realtime`.

### M2. Postgres exclusion is defensible, but Raven coupling needs guardrails

**Where:** `master.md:Scope`; `launch-prompt.md:Constraints`.

**Evidence:** The package excludes Postgres and stays on RavenDB (`master.md` line 50; `launch-prompt.md` line 30). The broader roadmap says Raven remains the primary managed-state/read-model store, but write-pattern rework is required because current writes are too chatty (`implementation-roadmap.md` lines 20-34).

**Recommendation:** Keep Postgres out of scope, but require new observer components to depend on journal/store abstractions rather than Raven sessions directly except in storage adapters.

### M3. Validation signals are not consistently objective

**Where:** `slices.md:Wave 1`, `Wave 4`, `Validation matrix`.

**Evidence:** Wave 1 requires `OnNewBlock` within `~1s of every real BSV mainnet block` (`slices.md` lines 42-44), which is not a practical finite completion signal. Wave 4 says metrics should "plausibly fire" (`slices.md` lines 133-136).

**Recommendation:** Replace fuzzy signals with bounded soak windows and thresholds: e.g. over 24h, observed N of N public block tips within p95 lag, and source counters match injected fixture observations exactly.

## Low / nits

### L1. Package mostly follows the skill, but the ledger is missing preferred columns

**Where:** `master.md:Program Ledger`; durable-wave-package `File Contracts`.

**Evidence:** The skill prefers ledger columns `slice | zone lead | owner | status | depends_on | validation | done_when`. The program ledger has `wave | slug | status | depends_on | done_when | audit` (`master.md` lines 167-175).

**Recommendation:** Add `zone lead/owner` and `validation` columns, or explain why the wave-level ledger intentionally differs.

### L2. Launch prompt says “without breaking public REST/SignalR API” while constraints allow a broadcast break

**Where:** `launch-prompt.md:Mission`, `Constraints`.

**Evidence:** Mission says “without breaking the public REST/SignalR API” (`launch-prompt.md` lines 5-9). Constraints then allow no backwards compatibility with legacy broadcast and a single `Broadcast` method change (`launch-prompt.md` lines 24-33).

**Recommendation:** Rewrite mission as “without breaking public APIs except the explicitly-versioned broadcast contract change.”

## What the program got right

The high-level sequencing of W1 headers, W2 mempool observation, W3 reorg, W4 metrics, and W5 broadcast cleanup is broadly sane for a stop-and-audit cadence. Reorg should not be built before the header chain exists, and source metrics should not be built before at least two or three sources are producing comparable observations.

Keeping Bitails and JungleBus realtime alive while P2P earns trust is the right operational posture. The program avoids the mistake of replacing known imperfect sources with a new source before it has lag, coverage, and failure evidence.

The durable package format is useful here. Given the previous thin-node audit found major issues in a smaller artifact, the audit gates are not ceremony for the risky waves. The problem is that the gates need sharper contracts and some missing production wave scope, not that the existence of gates is wrong.

## Recommended changes to the program before opening Wave 1

1. Add a Wave 0 or W1 prerequisite slice that freezes hub events, subscription group names, and `PeerSession` extension points for all planned waves.
2. Change Wave 5 dependencies from independent to `W2` or `W2,W4`; keep it last unless the API contract is split into Wave 0.
3. Add explicit W2 groundwork for `TxObservationSource.P2p`, source-neutral journal append, and watchlist correctness/scale tests.
4. Make W3 projection semantics a first-class slice, not a “tiny change.”
5. Add a production-ops wave or downgrade the program’s end-state claims.
6. Add repository-zone mapping and handoff requirements to `master.md`.

## Questions you couldn't answer from the package

- What concrete watchlist size is the HashSet design required to support?
- What is the intended post-reorg canonical lifecycle state: `Reorged`, `SeenInMempool`, or something else?
- Are block-body providers expected to return orphaned block bodies reliably after a reorg?
- What compatibility story do consumers get for the `Broadcast` return type change?
- What alerting backend, if any, should operator alerts target?
- Is inbound listener production integration intentionally out of scope, or just omitted?
