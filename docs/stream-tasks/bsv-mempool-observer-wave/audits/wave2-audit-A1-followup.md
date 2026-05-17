# Wave 2 Package Audit A1 Follow-up

Verdict: **MAJOR REVISION REQUIRED**

The revision closes the core S5 channel-race design and corrects the S1 parser rule. It also materially improves benchmark and live-evidence reproducibility. However, the package still has conflicting source-of-truth text for the Raven hot-reload mechanism and Bitails/JungleBus source-tag slice, and the payload-size policy still needs a precise config/ownership path before execution.

## A1 Closure Table

| A1 finding | Status | Where addressed | Residual concern |
|---|---|---|---|
| H1 - S5 races `IncomingMessages` | **closed** | `master.md:71-80`, `master.md:150-162`, `slices.md:359-487`, `launch-prompt.md:84-93`, parent `slices.md:150-154` | The race is closed by making `PerSessionFrameDispatcher` the single channel reader and moving `TxRelayCoordinator` to dispatcher subscriptions. A new dispatcher robustness concern is listed below as `new-M1`. |
| H2 - Raven hot-reload removal not implementable | **partially closed** | Corrected in `slices.md:216-244` with Raven Changes API; delete-path validation in `slices.md:248-260`; source pattern matches `src/Dxs.Consigliere/BackgroundTasks/StasAttributesChangeObserverTask.cs:45-50`; admin untrack deletes legacy docs at `src/Dxs.Consigliere/Data/Tracking/TrackedEntityRegistrationStore.cs:225-226` and `:261-262` | Higher-level docs still instruct the old Raven Subscription design: `master.md:58-60`, `master.md:128-132`, `launch-prompt.md:20-22`, and parent `slices.md:157-159`. Those are launch sources of truth and must be aligned to Changes API before S0 opens. |
| H3 - S1 P2PKH input matching wrong shape | **closed** | `slices.md:106-116`, `slices.md:123-131`, `slices.md:135-142`, `slices.md:551-555` | The corrected rule is right: parse scriptSig as `<sig> <pubkey>`, compute `HASH160(pubkey)`, and match that hash. Fixture coverage explicitly includes compressed and uncompressed pubkeys. |
| M1 - Benchmark reproducibility | **closed** | `slices.md:564-607`, `slices.md:622-630`, `launch-prompt.md:139-143` | The benchmark now separates matcher construction from lookup hot path and records host/runtime/corpus metadata. Cross-host comparability is acceptable because the canonical threshold is tied to the stated reference CPU class, with non-reference hosts recorded separately. |
| M2 - S6 source-tag conflict | **partially closed** | Corrected in `master.md:83-87`, `slices.md:489-539`, and ledger row `master.md:222` | Stale conflicting text remains: S0.2 still says S6 adjusts how runners populate `TxMessage.Source` (`slices.md:49-52`); Core Rule 6 still says Bitails/JungleBus tag through the new journal overload (`master.md:138-141`); launch end state repeats that (`launch-prompt.md:26-27`); parent still says runners get a production adjustment (`parent slices.md:164-166`). |
| M3 - Ownership-zone mismatches | **closed** | `master.md:168-190`, parent `slices.md:145-154` | The child and parent now agree that `SourceObservationRecorder` lives in Consigliere services, and `TxObservation.cs` is correctly mapped to `bsv-runtime-ingest`. |
| M4 - Payload-size policy missing | **partially closed** | `master.md:67-70`, `slices.md:292-313`, `slices.md:348-352` | The policy is explicit, but the implementation path is not fully owned. The package says `BsvP2pSetup` propagates the max into `PeerSessionConfig`, while the current session config is built in `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs:66-80`, and the config type at `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs:42-45` has no mempool fetch-size knob. Also, "timeout increments `OversizePayloadCount`" does not by itself distinguish oversize disconnect from a silent peer timeout. |
| M5 - S8 evidence schema thin | **closed** | `slices.md:658-688`, `slices.md:700-710`, `launch-prompt.md:142-145` | The evidence schema is now sufficient to verify `inv -> getdata -> tx -> match -> journal -> projection -> hub` from the evidence file alone, including commit/config/peer/timestamp/projection/payload fields. |

## New Findings

### new-M1 - Dispatcher fan-out lacks subscriber failure-isolation semantics

Where:
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:379-395`
- `docs/stream-tasks/bsv-mempool-observer-wave/slices.md:459-465`

Evidence:
- The dispatcher API says subscribers register `Func<InboundFrame, Task>` handlers and the dispatcher fans frames out to matching handlers sequentially.
- The validation tests cover one subscriber, two subscribers, and unsubscribe behavior, but not a throwing or slow subscriber.

Why it matters:
- The dispatcher becomes the shared delivery path for both `TxRelayCoordinator` and the mempool observer. If one handler throws and the dispatcher does not catch/log per subscriber, a mempool handler can terminate `RunAsync` or prevent relay handlers from receiving later frames. That recreates a different form of cross-consumer coupling after fixing the channel race.

Recommendation:
- Add explicit dispatcher semantics: each subscriber invocation is isolated with catch/log; a failed handler does not stop `RunAsync` and does not prevent later subscribers from receiving the frame.
- Add tests for a throwing subscriber and, if sequential fan-out is retained, define whether slow handlers are allowed to apply backpressure to all subscribers.

## New-Revision Consistency Checks

- Parent `slices.md` now agrees with the child package on `SourceObservationRecorder` placement and the dispatcher ownership model (`parent slices.md:145-154`).
- Parent `slices.md` still conflicts on Raven hot reload and Bitails/JBus production adjustment (`parent slices.md:157-166`).
- Core Rule 9 applies consistently to S5. S4 and S7 do not instruct implementers to read `IncomingMessages` directly; `rg` only finds the channel ownership discussion in S5 and the launch/core-rule sections.
- The H1 race-regression test is meaningful: it exercises `TxRelayCoordinator` and the mempool runner on the same session with mixed traffic and asserts neither path starves.

## Closing Rationale

S0 itself remains a narrow, implementable prerequisite slice. But the wave package still should not open S0 because the execution sources of truth disagree on two important downstream contracts: Raven Changes API versus Raven Subscription, and regression-pin-only S6 versus production migration to the new overload. Fix those stale sections, add the missing payload-size config ownership path, and define dispatcher failure isolation; after that, S0 can open.
