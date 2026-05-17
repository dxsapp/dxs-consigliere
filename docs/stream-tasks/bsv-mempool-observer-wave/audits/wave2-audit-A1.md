# Wave 2 Package Audit A1 - BSV Mempool Observer

Verdict: **MAJOR REVISION REQUIRED**

The package is directionally aligned with the parent program and correctly treats Wave 1 as a prerequisite, but it is not ready to open S0. The S0 slice itself is narrow and mostly tractable, yet the package contains several main-slice design defects that would force mid-wave rewrites or contract-pressure against the Wave 1 frozen surface.

## Findings

### H1 - S5 races existing `IncomingMessages` consumers when fetching tx payloads

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:292-303`
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs:238-250`
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs:316-350`
- `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:20-31`
- `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:93-99`

Evidence:
- S5 tells `P2pMempoolIngestRunner` to attach `OnInvReceived`, send `getdata`, then wait for the `tx` frame on `session.IncomingMessages`.
- The frozen W1 surface exposes callbacks for headers, inv, and reject only; there is no `OnTxReceived` callback.
- `PeerSession.ReceiveLoopAsync` dispatches additive callbacks and then writes every message to `IncomingMessages`.
- `TxRelayCoordinator` is already a long-lived singleton that reads `session.IncomingMessages.ReadAllAsync(...)` for every attached session.

Why it matters:
- `ChannelReader` is not a broadcast primitive. If S5 and `TxRelayCoordinator` both read from `IncomingMessages`, only one consumer gets each frame. A `tx` payload requested by S5 can be consumed by the relay coordinator, or S5 can consume relay/coordinator frames. This creates intermittent missed observations and test-only success depending on scheduling.
- Fixing this later by adding `OnTxReceived` would violate the W1 contract-freeze rule that W2 explicitly promises to preserve.

Recommendation:
- Revise S5 before opening the wave. Define one per-session dispatcher in Consigliere that owns `IncomingMessages` and routes frames to the relay coordinator and mempool observer, or explicitly extend an existing owner such as `TxRelayCoordinator` to route `tx` payloads without adding new `PeerSession` callbacks.
- Add an acceptance test that runs outgoing relay telemetry and P2P mempool fetch on the same session and proves no frame starvation or consumer race.

### H2 - Raven hot-reload removal semantics are not implementable as specified

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/master.md:58-60`
- `docs/stream-tasks/bsv-mempool-observer-wave/master.md:116-120`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:193-205`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:217-228`
- `src/Dxs.Consigliere/Data/TrackedEntityRegistrationStore.cs:207-226`
- `src/Dxs.Consigliere/Data/TrackedEntityRegistrationStore.cs:243-262`
- `src/Dxs.Consigliere/BackgroundTasks/StasAttributesChangeObserverTask.cs:45-50`

Evidence:
- S3 requires a Raven Subscription on `WatchingAddress` and `WatchingToken` and says add/remove/update changes hot-reload into the matcher.
- Current untrack paths delete the legacy `WatchingAddress` and `WatchingToken` documents.
- The repo's existing live-change pattern uses Raven Changes API and listens for `Put` and `Delete` events, not a Raven Subscription deletion stream.

Why it matters:
- As written, S3 depends on remove/tombstone behavior that is not tied to a concrete Raven mechanism. If deletes are not observed, untracked addresses and tokens remain in the hot matcher and W2 keeps reporting observations for things the operator removed.
- This is part of the mandatory watchlist-correctness slice in the parent program, so it cannot be left to implementation guesswork.

Recommendation:
- Replace the vague "Raven Subscription" requirement with a concrete mechanism that supports removals, such as Raven Changes API for `WatchingAddresses` and `WatchingTokens`, or a tracked-status/tombstone document stream whose update semantics are already explicit.
- Specify exact collection names, document-id keys, add/update/remove behavior, retry behavior after disconnect, and an integration test that exercises the existing admin untrack path and verifies matcher removal.

### H3 - S1 describes P2PKH input matching incorrectly

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:103-111`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:126-130`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:367-385`

Evidence:
- S1 says P2PKH input matching extracts Hash160 from "`OP_DUP OP_HASH160 ...`-style signature scripts."
- `OP_DUP OP_HASH160 <20-byte-hash> OP_EQUALVERIFY OP_CHECKSIG` is the standard P2PKH locking script shape, not the normal P2PKH unlocking/signature script shape.

Why it matters:
- The parent program makes address-input matching mandatory for W2. A correct P2PKH input match normally requires deriving Hash160 from the public key found in the scriptSig, or using previous-output script context. The current wording points implementers at a script pattern that will not appear in ordinary P2PKH inputs.
- This risks shipping output-only matching while believing input matching is covered.

Recommendation:
- Revise S1 to specify the exact input rule: parse standard P2PKH scriptSig, extract the pushed public key, compute `HASH160(pubkey)`, and match that against watched address hashes. If prevout context is required for any intended input class, say so explicitly and keep it out of S1 unless the wave also owns that data source.
- Add fixture cases for P2PKH input, P2PKH output, P2PK output if intended, token script output, malformed scripts, and a false-positive prefix collision.

### M1 - Benchmark thresholds are not reproducible as written

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/master.md:79-81`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:226-230`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:367-388`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:524-535`
- `docs/stream-tasks/bsv-mempool-observer-wave/launch-prompt.md:120-128`

Evidence:
- The package inherits 500K watchlist load <= 2s and p99 <= 100 ns hot-path targets.
- S7 specifies the target and synthetic corpus size, but does not lock down hardware, runtime, BenchmarkDotNet job, GC mode, warmup/iteration counts, process isolation, address/token distribution, or whether the 500K load includes Raven I/O or in-memory matcher construction only.

Why it matters:
- A p99 <= 100 ns target is sensitive to CPU, tiered JIT, runtime version, and benchmark harness shape. Without fixed assumptions, failures and passes are not comparable between operators.

Recommendation:
- Add a reproducibility block to S7 and the launch prompt: .NET SDK/runtime version, OS/arch, CPU class or reference host, Release configuration, BenchmarkDotNet job settings, warmups, iteration count, GC settings, and the generated corpus seed/distribution.
- Split the 500K load target into explicit phases if needed: Raven read, DTO normalization, and matcher construction.

### M2 - S6 conflicts with the current source-tag behavior and the S0 overload plan

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/master.md:72-75`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:49-52`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:332-346`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs:149-153`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs:175-180`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs:191`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/JungleBusRealtimeIngestRunner.cs:46-50`

Evidence:
- S0 says the existing `AppendAsync(TxMessage)` overload remains and Bitails/JungleBus continue to use it in W2.
- S6 says the Bitails/JungleBus runners pass source through the new journal overload.
- Current Bitails and JungleBus runners already construct `TxMessage` with `TxObservationSource.Bitails` and `TxObservationSource.JungleBus`.

Why it matters:
- This can turn S6 into churn for no behavior change, or worse, into a migration that bypasses existing `TxMessage` normalization and fingerprint behavior without a clear reason.

Recommendation:
- Make S6 explicitly a no-op plus regression-test slice unless inspection finds a concrete untagged path.
- If the intent is to migrate Bitails/JungleBus to the new overload, update S0 acceptance criteria to describe equivalent fingerprint, payload-reference, and duplicate semantics before requiring that migration.

### M3 - Ownership-zone mapping contains path and zone inaccuracies

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/master.md:143-152`
- `docs/repository-zones/zone-catalog.md:5-14`
- `docs/repository-zones/zone-catalog.md:16-20`
- `docs/stream-tasks/consigliere-thin-node-observer-program/slices.md:140-143`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:258-260`

Evidence:
- Wave 2 maps `src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs` to `indexer-state-and-storage`.
- The zone catalog maps `src/Dxs.Bsv/{BitcoinMonitor,Rpc,Zmq,Factories}/**` to `bsv-runtime-ingest`; the precedence note says `bsv-protocol-core` owns the rest of `src/Dxs.Bsv`, but not `BitcoinMonitor`.
- The parent Wave 2 slice list places `SourceObservationRecorder` under `src/Dxs.Bsv/P2p/Observer/`, while the child S4 places it under `src/Dxs.Consigliere/Services/P2p/`.

Why it matters:
- The repository instructions require determining zones before implementation. Incorrect zone names and parent/child path drift make the package harder to split and review correctly.

Recommendation:
- Correct the ownership table to map `TxObservation.cs` to `bsv-runtime-ingest`.
- Decide whether `SourceObservationRecorder` is protocol-side observer state or Consigliere orchestration state, then make parent program, child master, and S4 agree.

### M4 - Getdata fetch policy omits raw transaction payload-size handling

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:64-67`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:240-255`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:292-303`
- `src/Dxs.Bsv/P2p/Session/PeerSessionConfig.cs:33-38`
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs:441-452`
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs:489-496`

Evidence:
- S4/S5 make P2P raw transaction payload fetch central to the wave.
- `PeerSessionConfig.InitialMaxRecvPayloadLength` defaults to 2 MiB.
- `PeerSession.ReadNextFrameAsync` validates inbound payload size against `InitialMaxRecvPayloadLength`.
- `PeerSession` records peer `protoconf` max receive payload length, but the read path shown above does not use that value for inbound transaction frames.

Why it matters:
- BSV transactions can exceed 2 MiB. Without an explicit policy, W2 may silently fail to observe large mempool transactions even though the peer advertised support for larger payloads.

Recommendation:
- Add a S4/S5 acceptance rule for large transaction payloads: either configure and test a supported maximum, apply the negotiated protocol value where appropriate, or record oversize fetch failures as explicit unmatched/error evidence.
- Include a MiniBsvServer fixture with an over-2-MiB `tx` payload or an agreed maximum-size boundary test.

### M5 - S8 live-mainnet evidence is not sufficient to verify the path alone

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:418-432`
- `docs/stream-tasks/bsv-mempool-observer-wave/launch-prompt.md:128-145`

Evidence:
- S8 asks for txid, watched address, inv arrival timestamp, hub-fire timestamp, and final `SeenBySources`.
- It does not require commit SHA, runtime config, peer endpoint/count, correlation log lines, latency delta, projection sequence, raw payload reference, or whether Bitails/JungleBus also saw the transaction.

Why it matters:
- The operator-driven live path should be auditable from the evidence file. Current fields prove the final state, but not enough of the causal path from P2P inv to raw payload fetch to journal append to projection/hub update.

Recommendation:
- Extend S8 evidence with commit SHA, node config, peer count/endpoint, inv log line with txid, getdata/tx receipt log line, computed inv-to-hub delta in milliseconds, projection document id and last sequence, `PayloadAvailable`, raw payload reference, and final `SeenBySources`.

## Slice Scope Assessment

| Slice | Assessment |
| --- | --- |
| S0 - Contract/source model prep | Tractable, but package-level fixes should land before opening. The `TxObservationSource.P2p` constant, source-neutral journal overload, and projection test are correctly scoped. |
| S1 - Transaction parser | Not tractable as written. Output parsing is clear, but P2PKH input matching is specified with the wrong script shape. |
| S2 - Watchlist matcher | Mostly tractable. The HashSet-prefix plus full-hash-verify design matches the parent requirements and supports non-Raven unit tests. |
| S3 - Raven watchlist loader | Not tractable as written. Startup load is clear, but hot-removal semantics need a concrete Raven mechanism and collection/id contract. |
| S4 - P2P observation service | Partially tractable. Matching and journal append responsibilities are clear; payload-size policy and recorder ownership need tightening. |
| S5 - P2P mempool ingest runner | Not tractable as written. It needs a single-consumer frame-dispatch design before implementation can safely read tx payloads. |
| S6 - Existing source tags | Tractable after doc cleanup. Current Bitails/JungleBus runners already tag sources, so this should likely be regression coverage only. |
| S7 - Benchmark/scaffold | Partially tractable. The scenarios are right, but benchmark environment and job settings are underspecified. |
| S8 - Operator validation | Partially tractable. The procedure is realistic, but the evidence schema needs enough correlation data to prove the path. |

## Closing Rationale

Wave 1 is properly closed and W2 correctly depends on the frozen W1 surface. The S0 slice is narrow enough to implement after the package is corrected, but S0 should not open yet because downstream slices currently rely on an unsafe `IncomingMessages` consumption model, an underspecified Raven hot-reload mechanism, and an incorrect address-input parsing rule. Fixing those in the package now avoids pressure to amend the contract freeze during execution.
