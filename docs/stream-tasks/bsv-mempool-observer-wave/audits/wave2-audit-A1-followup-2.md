# Wave 2 Package Audit A1 Follow-up 2

Verdict: **APPROVE WITH CHANGES**

The second revision resolves the remaining package-level design blockers. The Raven hot-reload mechanism and S6 source-tag plan are now consistent across the child package and parent owned-paths block. The payload-size and dispatcher fixes are directionally correct, but two doc-precision issues should be corrected before S4/S5 execution so implementers do not reach for non-owned files or nonexistent `PeerSession` surface.

## Closure Table

| Finding | Status | Where addressed | Residual |
|---|---|---|---|
| H2 - Raven hot-reload removal not implementable | **closed** | Child scope now uses Raven Changes API at `master.md:58-65`; Core Rule 4 says Changes API and explicitly says Raven Subscription API is not used at `master.md:133-143`; launch end state says Changes API and Subscription API is not used at `launch-prompt.md:20-24`; S3 specifies `DocumentChangeTypes.Put` / `Delete` at `slices.md:216-246`; parent owned paths now say Changes API at parent `slices.md:157-162`. | No blocker. Ledger row `master.md:239` still says "subscription delta add/remove"; since the surrounding source-of-truth text is explicit, this is only wording cleanup. Prefer "Changes API delta" there. |
| M2 - S6 source-tag conflict | **closed** | S0.2 now says Bitails/JungleBus stay on `AppendAsync(TxMessage)` with no production-code change at `slices.md:49-54`; S6 is regression-pin-only at `slices.md:587-623`; master scope and Core Rule 6 agree at `master.md:88-92` and `master.md:149-162`; launch end state agrees at `launch-prompt.md:30-32`; parent owned paths agree at parent `slices.md:167-172`. | None. |
| M4 - Payload-size policy missing | **partially closed** | Config field and default are specified at `slices.md:300-317`; propagation through `BsvP2pHostedService` is specified at `slices.md:319-337`; the three outcomes and recorder counters are specified at `slices.md:339-357`. Current source confirms the path is implementable: `BsvP2pConfig` is the bound config type at `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs:10-45`, and `BsvP2pHostedService` constructs `PeerSessionConfig` at `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs:66-80`. | The docs still need two corrections: add `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` and `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs` to S4/S5 owned paths, and replace `LastDisconnectReason` at `slices.md:350-352` with the existing `session.Completion` result. `PeerSession` exposes `Completion` at `src/Dxs.Bsv/P2p/Session/PeerSession.cs:66` and completes it with the `DisconnectReason` at `src/Dxs.Bsv/P2p/Session/PeerSession.cs:568-570`; there is no `LastDisconnectReason` property. |
| new-M1 - Dispatcher failure isolation | **partially closed** | Required per-subscriber try/catch, continued `RunAsync`, later-subscriber delivery, sequential fan-out/backpressure policy, idempotent disposal, and subscribe-time tag are specified at `slices.md:445-482`; throwing and slow subscriber tests are specified at `slices.md:542-559`. | The primary API block still shows the old signature `Subscribe(string command, Func<InboundFrame, Task> handler)` at `slices.md:432-438`, while the new tagged API is described later at `slices.md:479-482`. Update the code block to `Subscribe(string command, string subscriberTag, Func<InboundFrame, Task> handler)`. The `subscriberTag` addition is useful for logs and tests; require non-empty stable tags to avoid low-value logs. |

## New Findings

No new findings beyond the residual documentation fixes in M4 and new-M1.

## Closing Rationale

The package no longer has a major design blocker. S0 may open after the small documentation fixes above are applied: align the M4 owned paths and `Completion`-based classification wording, and update the dispatcher API block to the tagged `Subscribe` signature. No further A1 follow-up is needed unless those fixes change the intended design.
