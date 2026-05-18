# Wave 6 — Pre-execution audit A1 prompt

Audit target: `docs/stream-tasks/production-ops-wave/` at the
W6-package-draft commit. Run async; revisions land in-wave.

---

You are auditing the wave-level plan for Wave 6 of
`consigliere-thin-node-observer-program` — the FINAL wave of the
program. Read:

- `docs/stream-tasks/production-ops-wave/master.md`
- `docs/stream-tasks/consigliere-thin-node-observer-program/master.md`
  (W6 row at line ~356 + §"Wave 6" prose at line ~304 +
  cross-wave dependencies §line ~317)
- `docs/stream-tasks/observation-source-metrics-wave/evidence/closeout.md`
  (closed wave; provides `SourceMetricsAggregator`,
  `SourceVisibilityTracker`)
- `docs/stream-tasks/broadcast-unification-wave/evidence/closeout.md`
  (closed wave; W5 broadcast contract migration is what W6
  documents)
- `docs/platform-api/thin-node-gate2-soak-runbook.md` (the
  runbook W6 cross-references)

Cross-validate against the actual repo state:

- `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` (the rotation host)
- `src/Dxs.Bsv/P2p/Pool/PeerRecord.cs` (data model)
- `src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs` (relay-back source)
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` (alert
  input surface)
- `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (where the new
  `Alert` + `Inbound` configs land)
- `src/Dxs.Consigliere/Services/Metrics/SourceMetricsAggregator.cs`
  (W4 hosted-poller template W6 mirrors)
- `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` +
  `AdminMetricsController.cs` (admin-endpoint conventions)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Scope coherence.** Does the slice ledger cover every W6
   done-when ("alert fires when pool drops below threshold in
   fixture; peer rotation evicts low-scoring peer in fixture;
   runbook reviewed")? Any program-stated requirement cut?

2. **Inbound listener decision.** Master.md §"Wave 6" allows
   "opt-in or off"; the W6 product decision commits to "off
   + config flag stub". Is shipping a config flag without
   implementing the actual listener acceptable, or should W6
   either implement it or drop the flag entirely?

3. **Score formula.** Master.md §"Scope" defines `score = 100 -
   latency_penalty - reject_penalty + relay_back_bonus`,
   clamped `[0, 100]`. Are the weights documented? Should the
   audit recommend specific numeric weights or leave them for
   the implementation slice?

4. **`MinimumScoreToRetain` default 30.** Out of 100 max
   possible. Justified? Operator-tunable in W6 or fixed?

5. **`AlertPollIntervalMs` default 60_000.** One-minute poll
   tick. Acceptable for ops resolution, or should the audit
   recommend tighter (e.g. 15s) given the W4 metrics aggregator
   ticks at 30s?

6. **Alert rule thresholds.** The 4 rules: pool-size,
   relay-back, reorg-depth, source-first-dropout. Are the
   numeric thresholds (relay-back < 30 %, source-first-dropout
   over 1 h) defensible? Should W6 audit recommend operator-
   tunable thresholds via config?

7. **Single-peer-per-tick eviction.** Master.md §Core Rule 5
   limits rotation to one peer per tick to avoid flap loops.
   Is that the right safety policy, or should the audit ask
   for a max-fraction-evicted-per-window constraint instead?

8. **Append-only alert events.** No SignalR push, no
   notification sinks (Slack / PagerDuty / webhook). Master.md
   §Out of scope explicitly defers these. Is the polling-only
   model sufficient for "production ops", or should W6 ship at
   least one push channel?

9. **SPA defer policy.** S8 alerts panel is deferred. Consistent
   with W2 S8 / W3 S7 / W4 S8 / W5 S7. Acceptable?

10. **Cross-wave references.** Master.md claims W4 + W5
    closure as prereq. W4 closed (`5a53e08`); W5 closed
    (`00f9cfc`). Confirm.

11. **`IPeerScoringPolicy` placement.** Lives in `Dxs.Bsv` per
    master.md ownership table (since `PeerManager` is in
    `Dxs.Bsv`). Is that the right layer, or should it live in
    `Dxs.Consigliere.Services.P2p` so the policy can read
    consigliere-level state?

12. **Doc deliverables.** Two new platform-api docs:
    `thin-node-prod-runbook.md` + `broadcast-w5-changeout.md`.
    Is the split correct, or should they be combined? Should
    a third doc — a final program closeout — also ship?

13. **No new contract surface.** Hub events, REST shapes,
    DTOs — master.md commits to read-only. Confirm by listing
    every type the wave will touch and noting which are new.

## Verdict format

End with:

```
Verdict: APPROVE | APPROVE WITH CHANGES | MAJOR REVISION REQUIRED
Critical findings: <count>
High findings: <count>
Medium findings: <count>
Low findings: <count>
Headline: <one sentence>
```

Then per-finding detail (`C1`, `H1`, `M1`, `L1` etc.):

- Severity
- Slice (or `wave-level`)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
