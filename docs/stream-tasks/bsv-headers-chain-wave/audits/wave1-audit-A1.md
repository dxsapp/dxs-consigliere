# Wave 1 Audit A1 — BSV Headers Chain + Program-Wide Contract Freeze

Verdict: **MAJOR REVISION REQUIRED**

## Findings

1. **H1 — S0 still does not freeze enough W6 peer-scoring telemetry**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/slices.md:26-44`; `docs/stream-tasks/consigliere-thin-node-observer-program/master.md:129-139`; `docs/stream-tasks/consigliere-thin-node-observer-program/audits/program-audit-A2.md:39-47`; `src/Dxs.Bsv/P2p/Session/PeerSession.cs:219-263`, `src/Dxs.Bsv/P2p/Session/PeerSession.cs:284-327`; `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:93-169`.

   **Evidence:** The wave adds `PeerTelemetry` fields for bytes, last send/recv, ping RTT p50/p95, getdata served count, reject count, and last disconnect reason (`slices.md:26-30`), plus sink methods for bytes, ping RTT, getdata served, reject, and disconnect (`slices.md:31-37`). That closes part of A2 N2, but it still omits the relay signals A2 explicitly named: tx relay timing, getdata request/serve timing, relay-back inv accounting, and per-class reject accounting. The current relay path records `GetDataServedAtMs` and relay-back invs in `TxRelayCoordinator` (`TxRelayCoordinator.cs:109-152`), not in the proposed `PeerTelemetry` snapshot. `RecordRejectReceived(RejectClass cls)` accepts a class, but the frozen snapshot collapses it to `RejectReceivedCount`, losing the class data W6 scoring would need.

   **Why it matters:** The package promises W6 will implement production peer scoring without reopening `PeerSession.cs` (`master.md:22-25`, `slices.md:9-14`). With the current S0 surface, W6 either has to reduce scoring to coarse bytes/RTT/disconnect data or add new telemetry later, which reintroduces the shared-file risk A2 N2 was meant to close.

   **Recommendation:** Either explicitly constrain W6 scoring to the listed fields, or expand S0 before opening it. Minimum additions should include per-peer tx relay acknowledgement timing, getdata requested/served counts and latency, relay-back inv count, reject counts by `RejectClass`, and protocol/decode violation count. The sink/snapshot contract should preserve those fields instead of accepting rich events and exposing only scalar totals.

2. **H2 — The wave relies on a `SendGetHeadersAsync` surface that does not exist**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/master.md:161-168`; `docs/stream-tasks/bsv-headers-chain-wave/slices.md:205-215`; `src/Dxs.Bsv/P2p/Session/PeerSession.cs:200-208`; `src/Dxs.Bsv/P2p/Messages/HeadersMessages.cs:14-48`; `src/Dxs.Bsv/P2p/P2pCommands.cs:34-36`.

   **Evidence:** The wave says it consumes the existing `PeerSession` Gate 1-3 surface `SendGetHeadersAsync` (`master.md:163-165`) and S3 says the service sends `getheaders` when it receives `inv(MSG_BLOCK)` (`slices.md:207-215`). The repo has `GetHeadersMessage` and `P2pCommands.GetHeaders`, but `PeerSession` only exposes send helpers for version, inv, getdata, notfound, headers, addr, tx, and getaddr (`PeerSession.cs:200-208`).

   **Why it matters:** S3 cannot be implemented as written, and because S0 is the only permitted place to add `PeerSession` extension points, leaving this out forces an immediate contract-freeze amendment after S0 or an unplanned PeerSession edit in S3.

   **Recommendation:** Add `SendGetHeadersAsync(GetHeadersMessage msg, CancellationToken ct)` to the S0 frozen surface and reflection/snapshot guard, or explicitly move that helper into S0's done criteria. If the service should use raw `SendAsync(P2pCommands.GetHeaders, ...)` instead, remove the false consumed-surface claim and say so in S3.

3. **H3 — Slice dependencies contradict the S0 prerequisite rule and miss real edges**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/master.md:92-100`, `docs/stream-tasks/bsv-headers-chain-wave/master.md:175-184`; `docs/stream-tasks/bsv-headers-chain-wave/slices.md:3-5`, `docs/stream-tasks/bsv-headers-chain-wave/slices.md:253-285`, `docs/stream-tasks/bsv-headers-chain-wave/slices.md:390-430`; `docs/stream-tasks/bsv-headers-chain-wave/launch-prompt.md:40-51`, `docs/stream-tasks/bsv-headers-chain-wave/launch-prompt.md:71-87`.

   **Evidence:** The text says every slice S1-S7 has `depends_on = S0` (`slices.md:3-5`, `launch-prompt.md:45-47`), but the ledger lists only S1 and S2 with direct S0 dependencies. S3 depends on S1/S2, S4 depends on S2, S5/S6 depend on S3, and S7 depends on S5 (`master.md:177-184`). The graph and launch prompt also disagree on S7: the graph shows S7 under S3 (`slices.md:415-421`), the launch prompt says S5/S6/S7 all depend on S3 (`launch-prompt.md:85-86`), while the ledger says S7 depends on S5 only. S4 is also under-specified: its bootstrapper feeds the chain plus store and is invoked by `HeadersChainService` startup (`slices.md:261-263`, `slices.md:276-282`), so S2 alone is not enough; it needs S1 and likely S3 for the integration path.

   **Why it matters:** The launch operator could open slices against the ledger and violate the prerequisite gate or start S4/S7 before their actual implementation dependencies are present.

   **Recommendation:** Encode direct S0 dependencies everywhere, not just transitively. For example: S3 `S0, S1, S2`; S4 `S0, S1, S2` plus `S3` if it wires service startup; S5 `S0, S3`; S6 `S0, S3`; S7 `S0, S5, S6` if the soak must validate both hub events and admin endpoint, otherwise remove the admin-endpoint assertion from S7 done criteria. Align the graph, ledger, and launch prompt to the same dependency set.

4. **H4 — S0 callback dispatch can silently break existing `IncomingMessages` consumers**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/slices.md:61-68`, `docs/stream-tasks/bsv-headers-chain-wave/slices.md:87-89`; `src/Dxs.Bsv/P2p/Session/PeerSession.cs:61`, `src/Dxs.Bsv/P2p/Session/PeerSession.cs:219-263`; `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:93-169`.

   **Evidence:** S0 tells the executor to parse `headers`, `inv`, and `reject` frames in `PeerSession` and invoke callbacks (`slices.md:63-68`). It does not explicitly say those frames must still be written to `IncomingMessages`. Today `IncomingMessages` is the public inbound channel (`PeerSession.cs:61`) and all non-internal frames are surfaced there (`PeerSession.cs:261-262`). The existing broadcast lifecycle reads that channel for `getdata`, relay-back `inv`, and `reject` (`TxRelayCoordinator.cs:98-158`).

   **Why it matters:** An implementation that treats the new callbacks as replacements instead of additive dispatch would regress already-landed Gate 3 broadcast behavior while still satisfying the new S0 reflection test.

   **Recommendation:** Add an explicit invariant to S0: callback dispatch is additive and must not remove `headers`/`inv`/`reject` from `IncomingMessages` until every existing consumer is intentionally migrated. Add a regression test that sends `inv`/`reject` and asserts both the callback fires and the inbound channel still receives the frame.

5. **M1 — The reflection contract-freeze test is too weak to prevent silent surface drift**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/slices.md:91-103`; `docs/stream-tasks/bsv-headers-chain-wave/launch-prompt.md:98-104`.

   **Evidence:** The validation only says to assert member existence via `typeof(...).GetMember(...)` and grep for old names. That catches deletion or rename, but not parameter type drift, return type drift, DTO constructor/property drift, adding unreviewed methods, or changing the `BroadcastReceiptDto` shape while keeping the type name.

   **Why it matters:** The wave's central safety claim is contract freeze. A member-existence smoke test can pass while the public or session contract still drifts in ways W2-W6 discover later.

   **Recommendation:** Use an exact contract snapshot/approval test. At minimum, reflect exact method names, return types, parameter types, DTO primary-constructor/property names and types, and assert no extra members on the frozen interfaces. Stronger: add a `PublicApi.Shipped.txt`/public API analyzer or a checked-in JSON manifest for `IWalletHub`, `IWalletServer` subscription methods, `BlockTipDto`, `ReorgEventDto`, `BroadcastReceiptDto`, `PeerSession` frozen members, `PeerTelemetry`, and `IPeerTelemetrySink`.

6. **M2 — The soak harness is close, but not fully reproducible as an audit artifact**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/master.md:26-27`, `docs/stream-tasks/bsv-headers-chain-wave/slices.md:349-388`; `docs/stream-tasks/consigliere-thin-node-observer-program/slices.md:79-90`.

   **Evidence:** The package defines the recorder shape, 1 s WhatsOnChain polling cadence, JSONL output, height join, and p50/p95/p99 analysis (`slices.md:357-370`). It does not define the JSONL schema, quantile formula, how to handle negative lag when P2P sees a block before WhatsOnChain, how to handle missing `OnNewBlock` or missing explorer samples, how HTTP errors/rate limits affect the denominator, or whether clocks must be NTP-synced before the run.

   **Why it matters:** The p95 <= 2 s threshold is only meaningful if independent executors compute the same result from the same timeline. The current wording leaves enough discretion to exclude bad samples or compute p95 differently.

   **Recommendation:** Add a small `README.md` or schema section for `HeadersSoakRecorder`: event record types, required fields, UTC clock/NTP requirement, join rules, negative-lag handling, missing-sample failure rules, HTTP-error reporting, quantile algorithm, and minimum expected block count for the run.

7. **M3 — W2-W6 handoff facts are not mapped to downstream consumers, and W5's server contract remains ambiguous**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/master.md:148-159`; `docs/stream-tasks/bsv-headers-chain-wave/launch-prompt.md:110-118`; `docs/stream-tasks/consigliere-thin-node-observer-program/master.md:113-119`, `docs/stream-tasks/consigliere-thin-node-observer-program/master.md:290-302`; `src/Dxs.Consigliere/WebSockets/IWalletServer.cs:36-39`; `src/Dxs.Consigliere/WebSockets/WalletHub.cs:129-147`.

   **Evidence:** The handoff list names frozen hub signatures, callbacks, telemetry, header schema, and config keys, but it does not say which wave consumes each fact or what compatibility guarantee each consumer can rely on (`master.md:148-159`). W5 is the ambiguous case: the parent program says `Broadcast(hex) -> BroadcastReceiptDto` is finalised, while the hub method itself changes in W5 (`program master.md:113-119`). Current code has `IWalletServer.Broadcast(...) -> Task<bool>` and a separate `BroadcastTracked(...) -> Task<BroadcastReceiptDto>` (`IWalletServer.cs:36-39`, `WalletHub.cs:129-147`). Wave 1 only freezes a comment on `BroadcastReceiptDto` (`slices.md:57-59`), so it is not clear whether W5 is expected to add/change server methods outside S0 or only consume a DTO shape.

   **Why it matters:** Downstream wave planning should not have to reopen Wave 1 to discover whether a server hub signature is frozen or intentionally deferred. Ambiguous handoffs weaken the package's stop-and-audit discipline.

   **Recommendation:** Replace the generic handoff list with a W2-W6 table. For each wave, list the exact consumed type/member, allowed changes, and forbidden changes. For W5, state explicitly: either S0 freezes only `BroadcastReceiptDto` and W5 is allowed to change `IWalletServer`/`WalletHub.Broadcast`, or S0 must also add the vnext `Broadcast(hex) -> BroadcastReceiptDto` server signature/stub.

## Path Spot-Check

The named current files exist and the wave generally points at the right implementation areas:

- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` exists and currently exposes `IncomingMessages`, typed send helpers, and `OnAddrReceived` (`PeerSession.cs:61`, `PeerSession.cs:200-215`).
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs`, `WalletHub.cs`, and `BroadcastReceiptDto.cs` exist with the current callback/server-method split (`IWalletHub.cs:7-19`, `WalletHub.cs:129-147`, `BroadcastReceiptDto.cs:3-8`).
- `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` exists and currently only takes `BsvP2pHealth`; S6 will need to extend constructor dependencies consistently with controller conventions (`AdminP2pController.cs:19-23`).
- `src/Dxs.Bsv/P2p/**` is consistent with the parent program's `bsv-protocol-core` mapping via the zone-catalog precedence rule assigning non-runtime `src/Dxs.Bsv` paths to protocol core (`zone-catalog.md:16-20`).

## Closing Rationale

The package is directionally close: the scope carve-outs are mostly clear, S0 is correctly identified as the prerequisite, and the owned paths line up with the current repo layout. It should not open S0 yet. The contract-freeze slice is the program's safety valve, and it still misses concrete PeerSession and telemetry details, relies on a non-existent getheaders helper, and has dependency contradictions that could send execution into the wrong order. Fix those in the docs first, then S0 can be audited as a bounded implementation slice.
