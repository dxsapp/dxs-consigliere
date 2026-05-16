# Wave 1 Audit A1 Follow-Up 2 — BSV Headers Chain + Program-Wide Contract Freeze

Verdict: **APPROVE**

## Follow-Up Closure Table

| Follow-up finding | Status | Where revision addresses it | Residual concern |
|---|---|---|---|
| new-H1 — Telemetry registry design could not be wired as written | **Closed** | `master.md:114-122`, `master.md:151-153`, `master.md:183-186`; `slices.md:100-148`, `slices.md:248-281`; `launch-prompt.md:27-34` | None. The registry indirection is removed. S0 now freezes a per-session `PeerSession.Telemetry` sink and routes relay telemetry through direct `session.Telemetry.<...>` calls from `TxRelayCoordinator`, which already processes frames with the originating session in scope (`TxRelayCoordinator.cs:93-169`). `PeerManager.cs` no longer needs S0 edits; current source confirms it is only the construction site for sessions (`PeerManager.cs:170-172`), and W6 is explicitly allowed to swap the construction site later. |
| new-M1 — S0 blurred typed client callbacks with `WalletHub` server methods | **Closed** | `slices.md:150-193`, `slices.md:257-263`; `launch-prompt.md:134-144`; `master.md:114-116` | None. The revision now states `IWalletHub` owns typed client callbacks (`OnNewBlock`, `OnReorg`), `IWalletServer` / `WalletHub` own only server-callable subscription methods, and actual broadcast emission is deferred to S5's `HubNewBlockNotifier` via `IHubContext<WalletHub, IWalletHub>`. Current source matches that split: `IWalletHub` is the client callback interface (`IWalletHub.cs:7-18`), and `WalletHub` implements server methods through `IWalletServer` (`WalletHub.cs:20-24`, `IWalletServer.cs:10-40`). |

## New Findings

None.

## Closing Rationale

The follow-up revision resolves the two blockers from `wave1-audit-A1-followup.md` without introducing a new execution-safety issue. The S0 contract-freeze slice is now specific enough to open: it has a bounded ownership set, an implementable telemetry path, a clear SignalR client/server contract split, additive-dispatch protection, and manifest-based contract drift validation.

S0 is ready to open, subject to the existing requirement that `audits/S0-A1.md` runs after the S0 implementation lands and before S1-S7 begin.
