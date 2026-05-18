# Wave 6 — Pre-Execution Audit A1

Audit target: `docs/stream-tasks/production-ops-wave/` at commit `3daf4ce`.

Verdict: MAJOR REVISION REQUIRED
Critical findings: 1
High findings: 2
Medium findings: 3
Low findings: 2
Headline: W6 is directionally coherent, but two planned alert rules and the peer-rotation fixture are not implementable as specified against the current W2/W4/W5 surfaces.

## C1

- Severity: CRITICAL
- Slice: S2
- Issue: `RelayBackRateBelowThreshold` is specified as "relay-back rate < 30% in the last poll window" averaged via `PeerTelemetry.RelayBackInvCount` (`docs/stream-tasks/production-ops-wave/master.md:77`), but `PeerTelemetry` only exposes a lifetime relay-back count and no denominator/window (`src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs:18`). Core Rule 4 also forbids new counters in W6 (`docs/stream-tasks/production-ops-wave/master.md:159`), so the rule cannot be computed as written.
- Recommended fix: Revise S2 before implementation. Either compute the windowed rate from existing persisted `OutgoingTransaction.PeerAttempts` (`RelayBackAtMs` divided by announced attempts in the window) or explicitly amend Core Rule 4 to allow a W6-owned relay-announcement recorder. Pin the denominator, window, and no-traffic behavior in `P2pAlertPoller_RelayBackRateBelowThreshold_FiresEvent`.

## H1

- Severity: HIGH
- Slice: S2
- Issue: `SourceFirstDropout` is specified as "zero FirstSeen increments in the last hour (via the W4 visibility tracker)" (`docs/stream-tasks/production-ops-wave/master.md:84`), but `SourceVisibilityTracker` exposes only cumulative counters through snapshots (`src/Dxs.Consigliere/Services/Metrics/SourceVisibilityTracker.cs:176`, `src/Dxs.Consigliere/Data/Models/Metrics/SourceMetricsSnapshot.cs:63`). Reading the tracker directly cannot tell whether a source incremented in the last hour.
- Recommended fix: Change S2 to read W4 `SourceMetricsSnapshot` history, compare latest `VisibilityCounters[source].FirstSeen` against the snapshot at or before `now - SourceFirstDropoutWindow`, and fire only when the delta is zero. Add the required snapshot-history repository seam and fixture coverage.

## H2

- Severity: HIGH
- Slice: S1 / S6
- Issue: The plan says the fixture will drive `PeerManager.TickAsync` with seeded peer records (`docs/stream-tasks/production-ops-wave/master.md:112`), but `TickAsync` is private and returns immediately when the pool is full (`src/Dxs.Bsv/P2p/Pool/PeerManager.cs:124`, `src/Dxs.Bsv/P2p/Pool/PeerManager.cs:136`). The only connect path constructs a live `PeerSession` and performs network I/O (`src/Dxs.Bsv/P2p/Pool/PeerManager.cs:170`), so the promised rotation fixture is not deterministic without an additional seam.
- Recommended fix: Add an S0/S1 testability requirement: extract a pure/internal `PeerRotationPlanner` or expose an internal `TickOnceAsync` plus an injectable peer connector/session factory. The fixture should seed active peers without sockets and assert one eviction per tick before any refill attempt.

## M1

- Severity: MEDIUM
- Slice: S0 / S1
- Issue: `PeerScore` is planned as data carried in `PeerRecord` (`docs/stream-tasks/production-ops-wave/master.md:57`), but `PeerRecord` currently has only attempt aggregates such as `SuccessCount`, `FailCount`, and `MeanLatencyMs` (`src/Dxs.Bsv/P2p/Pool/PeerRecord.cs:30`). Reject and relay-back inputs live on active `PeerSession.Telemetry` snapshots (`src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs:30`), so the score input and update lifecycle are underspecified.
- Recommended fix: Keep `IPeerScoringPolicy` in `Dxs.Bsv` only if it is pure and consumes BSV-layer data, e.g. `PeerScoreInput(PeerRecord record, PeerTelemetry? telemetry)`. Define when `PeerRecord.Score` is recalculated and persisted, and how disconnected peers are scored when no live telemetry exists.

## M2

- Severity: MEDIUM
- Slice: S0
- Issue: The score formula is named, but the numeric latency/reject/relay-back weights are not locked (`docs/stream-tasks/production-ops-wave/master.md:57`). `MinimumScoreToRetain = 30` is also introduced without rationale (`docs/stream-tasks/production-ops-wave/master.md:68`) while runtime score tuning is explicitly out of scope (`docs/stream-tasks/production-ops-wave/master.md:140`).
- Recommended fix: Before S0 lands, document concrete default weights, penalty caps, relay-back bonus cap, and the rationale for the 30/100 retain threshold. Pin those values in `PeerScoringTests`; if operators are expected to tune them, add config now instead of a fixed policy.

## M3

- Severity: MEDIUM
- Slice: S2 / S4
- Issue: W6 makes the poll interval configurable, but most alert thresholds are hard-coded in the plan: relay-back `<30%`, degraded-reorg lookback `5 minutes`, and source-first-dropout `1 hour` (`docs/stream-tasks/production-ops-wave/master.md:73`, `docs/stream-tasks/production-ops-wave/master.md:77`, `docs/stream-tasks/production-ops-wave/master.md:81`, `docs/stream-tasks/production-ops-wave/master.md:84`). For an operator-facing production wave, these should be deployment knobs, not embedded constants.
- Recommended fix: Add a nested `BsvP2pConfig.Alert` section with `PollIntervalMs`, `RetentionCount`, `MinPoolSize`, `RelayBackMinPercent`, `RelayBackWindowMs`, `DegradedReorgLookbackMs`, and `SourceFirstDropoutWindowMs`. Tests should assert defaults and bounded/clamped behavior.

## L1

- Severity: LOW
- Slice: S5
- Issue: W6 intentionally cross-references `thin-node-gate2-soak-runbook.md` without rewriting it (`docs/stream-tasks/production-ops-wave/master.md:145`), but that runbook still says outgoing broadcast and `TxRelayCoordinator` are not wired (`docs/platform-api/thin-node-gate2-soak-runbook.md:87`). That text is now stale after W2/W5 and can mislead operators if linked without context.
- Recommended fix: Keep the old runbook as historical Gate 2 evidence, but make `thin-node-prod-runbook.md` explicitly state that Gate 2's "not tested" section is obsolete for post-W5 production and list the current canonical broadcast path.

## L2

- Severity: LOW
- Slice: wave-level / S5
- Issue: The parent program still has `Program closeout commit: (pending)` (`docs/stream-tasks/consigliere-thin-node-observer-program/master.md:416`) and the program DoD requires closeout evidence plus public API notes (`docs/stream-tasks/consigliere-thin-node-observer-program/master.md:372`). W6 S5 names the prod runbook and W5 changeout doc, but not the final program closeout update.
- Recommended fix: Add a W6 closeout task to update the parent program ledger/status and produce final program closeout evidence after S1-S7 close. This can remain separate from the two platform-api docs.

## Accepted Decisions

- The inbound-listener decision is acceptable as an explicit off-by-default/no-op stub, provided `Inbound.Enabled = true` logs a warning and `BsvP2pHealth.InboundEnabled` reflects the config.
- One-peer-per-tick eviction is the right first safety policy for the current pool size; a max-fraction-per-window constraint is unnecessary unless future target pool sizes grow materially.
- Append-only Raven alert events plus REST polling are acceptable for W6, matching the W4 source-metrics pattern.
- Deferring the SPA page is consistent with the W2 S8 / W3 S7 / W4 S8 / W5 S7 operator-defer pattern.
- Cross-wave prerequisites are satisfied: W4 closed at `5a53e08`; W5 closed at `00f9cfc`.
