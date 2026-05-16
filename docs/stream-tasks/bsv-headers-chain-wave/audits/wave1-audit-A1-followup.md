# Wave 1 Audit A1 Follow-Up — BSV Headers Chain + Program-Wide Contract Freeze

Verdict: **MAJOR REVISION REQUIRED**

## A1 Closure Table

| A1 finding | Status | Where revision addresses it | Residual concern |
|---|---|---|---|
| H1 — S0 did not freeze enough W6 peer-scoring telemetry | **Partially closed** | `master.md:14-30`, `master.md:61-63`, `master.md:107-119`, `master.md:171-188`; `slices.md:52-141`, `slices.md:672-679`; `launch-prompt.md:27-32` | The field/method coverage is now substantially stronger: getdata requested/served, serve latency, relay-back invs, reject by class, protocol violations, disconnects, and registry are declared. The new registry wiring is incomplete because `PeerManager` is not owned by S0 even though the registry must be populated when sessions become ready; see new H1 below. |
| H2 — Wave relied on missing `SendGetHeadersAsync` | **Closed** | `master.md:61-63`, `master.md:149`, `master.md:202`; `slices.md:42-50`, `slices.md:358-366`, `slices.md:253-254`; `launch-prompt.md:21-26`, `launch-prompt.md:139-140` | None. The helper is explicitly added in S0 and S3 depends on it. |
| H3 — Slice dependencies contradicted S0 prerequisite rule | **Closed** | `master.md:97-100`, `master.md:198-209`; `slices.md:5-8`, `slices.md:262`, `slices.md:322`, `slices.md:360`, `slices.md:412-413`, `slices.md:448`, `slices.md:478`, `slices.md:509-510`, `slices.md:605-655`; `launch-prompt.md:60-77`, `launch-prompt.md:112-117` | None. The ledger, per-slice headers, graph, and launch prompt now agree on direct S0 edges and the additional S4/S7 dependencies. |
| H4 — Callback dispatch could break `IncomingMessages` consumers | **Closed** | `master.md:81-91`, `master.md:107-113`, `master.md:224-225`; `slices.md:20-40`, `slices.md:202-213`, `slices.md:241-249`, `slices.md:676-677`; `launch-prompt.md:21-24`, `launch-prompt.md:84-87` | None. The invariant and regression test are concrete. Current code confirms `IncomingMessages` is the existing channel (`PeerSession.cs:61`) and `TxRelayCoordinator` still depends on it (`TxRelayCoordinator.cs:93-169`). |
| M1 — Reflection contract-freeze test was too weak | **Closed** | `master.md:78-79`, `master.md:136-140`, `master.md:223`; `slices.md:175-200`, `slices.md:241-247`, `slices.md:672-678`; `launch-prompt.md:125-136` | None. The manifest/approval test is the right guard for signatures, DTO shapes, and accidental extra members. |
| M2 — Soak harness was not fully reproducible | **Closed** | `master.md:28-30`, `master.md:75-77`, `master.md:228-229`; `slices.md:516-588`, `slices.md:590-603`; `launch-prompt.md:137-145` | None. JSONL schema, clock requirements, join rules, negative-lag handling, HTTP-error handling, quantile algorithm, and pass/fail thresholds are now specified. |
| M3 — Handoff facts were generic and W5 server contract was ambiguous | **Partially closed** | `master.md:67-69`, `master.md:164-191`; `slices.md:162-173`; `launch-prompt.md:33-35`, `launch-prompt.md:96-98` | The W2-W6 handoff table and W5 exception close the original ambiguity. One hub-contract wording issue remains: S0 still says `WalletHub.cs` should contain an `OnNewBlock` / `OnReorg` body (`slices.md:153-155`), but current `WalletHub` is the server hub and `IWalletHub` is the typed client callback contract (`IWalletHub.cs:7-18`, `WalletHub.cs:20-24`). The implementation should broadcast via `HubNewBlockNotifier` / `IHubContext<WalletHub, IWalletHub>` in S5, while S0 only adds client callback signatures and subscription server methods. |

## New Findings

1. **H1 — New telemetry registry design cannot be wired as written**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/slices.md:100-141`, `docs/stream-tasks/bsv-headers-chain-wave/slices.md:215-224`; `docs/stream-tasks/bsv-headers-chain-wave/master.md:147-153`, `docs/stream-tasks/bsv-headers-chain-wave/master.md:200-202`; `src/Dxs.Bsv/P2p/Pool/PeerManager.cs:170-172`.

   **Evidence:** The revision adds `IPeerTelemetryRegistry` and says it maps `IPEndPoint -> IPeerTelemetrySink`, is populated by `PeerManager` when a session reaches `Ready`, and is used by `TxRelayCoordinator` for getdata / relay-back telemetry (`slices.md:121-140`). But S0 owned paths include `PeerSession.cs`, new chain telemetry files, and `TxRelayCoordinator.cs`; they do **not** include `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` (`slices.md:215-224`, `master.md:147-153`). Current `PeerManager` is the code that creates `PeerSession` (`PeerManager.cs:170-172`), so it is the only place that can reliably allocate/register the same per-peer sink before the session is used.

   **Why it matters:** As written, S0 can compile a registry and add `TxRelayCoordinator` sink calls, but the registry cannot be populated with the same sink held by the active `PeerSession`. The default registry would return `NullPeerTelemetrySink`, meaning the W6 telemetry path appears frozen but loses the relay signals H1 was meant to preserve.

   **Recommendation:** Either add `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` to S0 ownership and specify exact registry wiring, or simplify the design so `TxRelayCoordinator` uses `session.Telemetry` directly and no registry is needed. If keeping the registry, S0 should state how `PeerManager` obtains a sink for each endpoint, passes it to `PeerSession`, registers/unregisters it on ready/completion, and tests that `TxRelayCoordinator` records into the same sink.

2. **M1 — S0 still blurs typed client callbacks with `WalletHub` server methods**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/slices.md:143-160`; `docs/stream-tasks/bsv-headers-chain-wave/slices.md:446-474`; `src/Dxs.Consigliere/WebSockets/IWalletHub.cs:7-18`; `src/Dxs.Consigliere/WebSockets/IWalletServer.cs:10-40`; `src/Dxs.Consigliere/WebSockets/WalletHub.cs:20-24`, `src/Dxs.Consigliere/WebSockets/WalletHub.cs:129-154`.

   **Evidence:** S0.8 says `WalletHub.cs` gets an `OnNewBlock` body that calls `Clients.Group("block:tip").OnNewBlock(tip)` and an `OnReorg` no-op body (`slices.md:153-155`). In the current repo shape, `IWalletHub` is the typed **client callback** interface, while `WalletHub` implements `IWalletServer` server-callable methods (`WalletHub.cs:20-24`). Existing client callbacks like `OnBroadcastStateChanged` exist only on `IWalletHub` (`IWalletHub.cs:14-18`), while server-callable broadcast/subscription methods live on `WalletHub` / `IWalletServer` (`IWalletServer.cs:36-40`, `WalletHub.cs:129-154`). S5 already has the right pattern: `HubNewBlockNotifier` uses `IHubContext<WalletHub, IWalletHub>` to broadcast (`slices.md:450-459`).

   **Why it matters:** An executor following S0 literally could add public `OnNewBlock` / `OnReorg` server methods to `WalletHub`, exposing a callable hub method instead of freezing only the client callback contract. That is contract drift in the public realtime surface.

   **Recommendation:** Change S0.8 to say: add `OnNewBlock` and `OnReorg` only to `IWalletHub`; add only `SubscribeToBlockTip` and `SubscribeToReorg` to `IWalletServer` / `WalletHub`; implement actual broadcasts in S5's `HubNewBlockNotifier` via `IHubContext<WalletHub, IWalletHub>`. The manifest should reflect this split.

## Closing Rationale

The revision closes most of A1. The dependency graph, soak harness, additive-dispatch guard, manifest test, `SendGetHeadersAsync`, and W5 DTO/server-method handoff are all materially better.

S0 is **not ready to open** yet. The new telemetry registry must be made implementable before S0 starts, and the hub callback/server-method wording should be corrected so the frozen realtime contract lands in the right place.
