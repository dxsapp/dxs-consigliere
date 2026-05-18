# Wave 6 — S1 + S2 slice-audit prompt

Audit target: W6 S1 + S2 at commit `8fcfeb6` on
`codex/consigliere-vnext`. Run sync. S3+ open after sign-off.

---

You are auditing two slices of Wave 6 (`production-ops-wave`):
**S1** (peer rotation) and **S2** (alert poller). Both are
shipped + green; this is a quality gate before S3 opens.

Read:

- `docs/stream-tasks/production-ops-wave/master.md`
  (post-A1 revised package + Definition of Done + Slice
  Ledger)
- `docs/stream-tasks/production-ops-wave/audits/wave6-audit-A1-followup.md`
- `docs/stream-tasks/production-ops-wave/audits/wave6-audit-A1-followup-2.md`
  (the H1 zero-sample-window suppression S2 must honour)

Cross-validate against the actual repo state at `8fcfeb6`:

**S1 surface:**

- `src/Dxs.Bsv/P2p/Pool/PeerRotationPlanner.cs` (planner +
  `PeerRotationPolicy` + `RotationDecision` + `ScoredPeer`)
- `src/Dxs.Bsv/P2p/Pool/PeerManagerConfig.cs` (new
  `RotationPolicy` optional field)
- `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` (edit — `EvictByRotationPlanner` invoked from `TickAsync`)
- `tests/Dxs.Bsv.Tests/P2p/Pool/PeerRotationPlannerTests.cs`

**S2 surface:**

- `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (new
  `AlertConfig` nested section)
- `src/Dxs.Consigliere/Data/Models/P2p/P2pAlertEvent.cs` +
  `P2pAlertType`
- `src/Dxs.Consigliere/Data/P2p/{IAlertEventRepository,RavenAlertEventRepository}.cs`
- `src/Dxs.Consigliere/Services/Metrics/ISnapshotPersistence.cs` (new
  `GetSnapshotsInWindowAsync`)
- `src/Dxs.Consigliere/Services/Metrics/RavenSnapshotPersistence.cs` (impl)
- `src/Dxs.Consigliere/Services/P2p/P2pAlertEvaluator.cs`
- `src/Dxs.Consigliere/Services/P2p/P2pAlertPoller.cs`
- `tests/Dxs.Consigliere.Tests/P2p/P2pAlertEvaluatorTests.cs`
- `tests/Dxs.Consigliere.Tests/P2p/P2pAlertPollerTests.cs`
- `tests/Dxs.Consigliere.Tests/Metrics/SourceMetricsAggregatorTests.cs`
  (W4 fake extended for the new interface method — no behavioural change)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

### S1 — rotation

1. **Planner purity.** Does `PeerRotationPlanner.Plan` have
   any I/O / async / mutable state? Tests claim it's
   pure-logic.

2. **Single-peer-per-tick (Core Rule §5).** Does the planner
   guarantee at most one key in `RotationDecision.EvictKeys`
   no matter how many sub-floor peers exist? Pinned in
   `OnlyOnePeerEvictedPerTick_WhenMultipleBelowFloor`?

3. **Floor semantics.** Is "MinimumScoreToRetain" treated as
   strict `<` (peer at exact floor RETAINED)? Pinned in
   `PeerExactlyAtFloor_IsNotEvicted`?

4. **Zero-floor disable.** With `MinimumScoreToRetain = 0`,
   does the planner truly become a no-op? Pinned in
   `FloorAtZero_NeverEvicts`?

5. **PeerManager integration race.** `EvictByRotationPlanner`
   reads `_active.ToArray()`, scores via
   `session.Telemetry.Snapshot()`, then `_active.TryRemove`.
   Between the snapshot and the remove, the session could
   have disconnected on its own. Is the
   `Completion.ContinueWith` reaper compatible (no
   double-dispose)?

6. **Telemetry fault isolation.** If one peer's
   `Telemetry.Snapshot()` throws, does the planner still see
   the other peers? Pinned?

7. **Eviction-then-replenish ordering.** Does the integrated
   `TickAsync` evict BEFORE computing deficit so the evicted
   slot is refilled this same tick?

8. **Optional-DI back-compat.** With `scoringPolicy == null`
   OR `config.RotationPolicy == null`, does
   `EvictByRotationPlanner` short-circuit without any side
   effect (legacy behaviour preserved)?

### S2 — alerts

9. **Formula fidelity per rule.** Each of the 4 rules:
   - `PoolSizeBelowThreshold`: strict `<`?
   - `RelayBackRateBelowThreshold`: strict `<`? Both gate
     conditions present (`sum(ΔGetData) > 0` AND
     `rate < threshold`)?
   - `ReorgDepthExceeded`: `ageMs <= ReorgDepthWindowMs`
     (inclusive)? Negative age (future-stamped reorg)
     guarded?
   - `SourceFirstDropout`: needs `>= 2` snapshots AND
     `maxDelta > 0` AND per-source `delta == 0`?

10. **Zero-sample-window pin (A1 pass-2 H1).** Does
    `Rule2_RelayBackRate_ZeroSampleWindow_DoesNotFire`
    actually fail under the old `max(1, ΔGetData)` formula
    (i.e., would a regression be caught)?

11. **Baseline tick semantics.** First tick after startup
    records baseline only. Is this universal across rules
    (or specific to RelayBackRate)? Verify the other 3
    rules DO fire on the first tick when their preconditions
    hold.

12. **Counter-reset clamp.** On peer reconnect the lifetime
    counter snapshots reset to 0. Is the per-peer Δ clamped
    `Math.Max(0, …)` so the cumulative-zero-then-low number
    doesn't corrupt the rate?

13. **Unique-id collision.** Multiple rules firing on the
    same tick: does each event get a unique
    `p2p/alerts/{unixMs:D14}` id via the `idOffset` increment?

14. **SourceFirstDropout `MaxDelta == 0` semantics.** The
    "system-wide quiet" exemption — is the test pin actually
    against the exact `maxDelta == 0` check, not some
    side-effect that hides it?

15. **Append-only invariant.** Does the poller ever update
    an existing event's id (mutating an active alert)? The
    `Stored[id] = …` pattern in the fake repo would mask
    such a bug — does the real `RavenAlertEventRepository`
    refuse to overwrite via `StoreAsync` semantics?

16. **Retention eviction off-by-one.** With
    `AlertRetentionEvents = 2` and 6 events present after
    a tick, does the poller delete exactly 4 oldest?

17. **Snapshot-store fault tolerance.** When
    `GetSnapshotsInWindowAsync` throws, does the poller
    still write Pool / Reorg / RelayBack alerts that tick?
    The
    `TickOnceAsync_SnapshotStoreFault_DoesNotPoisonOtherRules`
    pins this — is the catch broad enough?

18. **AlertConfig disable.** With `AlertConfig.Enabled =
    false`, does the hosted-service `StartAsync` return
    cleanly without spinning the loop? Is `TickOnceAsync`
    still usable as a test seam in that mode (it bypasses
    the gate by design)?

19. **Health-unbound behaviour.** With `BsvP2pHealth` not
    bound to a `PeerManager`, `ActiveSessions` is empty,
    `PoolSize` is 0, `LastDegradedReorgAt` is null. Does the
    poller behave sanely (only the pool-size rule fires
    against the default threshold)?

20. **Doc-id format consistency.** Master.md §"Critical
    alert poller" specifies `p2p/alerts/{unixMs:D14}`. Does
    `P2pAlertEvent.BuildId` match? Will `D14` ever truncate
    a real Unix-ms timestamp (which is 13 digits today,
    14 digits from year ~2286)?

### Cross-slice

21. **Surface freeze.** Did either slice add a new
    contract-bearing type besides what S0+S1+S2's wave-A1
    package authorised? (Frozen list:
    `PeerScore`/`IPeerScoringPolicy`/`DefaultPeerScoringPolicy`
    for S0; `PeerRotationPlanner`/`PeerRotationPolicy`/
    `RotationDecision`/`ScoredPeer` for S1; `P2pAlertEvent`/
    `P2pAlertType`/`IAlertEventRepository`/`RavenAlertEventRepository`/
    `P2pAlertEvaluator`/`P2pAlertEvaluatorInput`/`P2pAlertPoller`
    for S2; new `AlertConfig` section; new
    `ISnapshotPersistence.GetSnapshotsInWindowAsync` method.)

22. **No DI wiring yet.** Both slices ship the types; the
    `BsvP2pSetup` registration is deferred to S7. Confirm
    neither slice introduced production `services.AddX`
    calls.

23. **W4 fake compat.** The
    `SourceMetricsAggregatorTests.FakePersistence` got a
    new method implementation. Is the implementation
    behaviourally equivalent to the production semantics
    (query by `SnapshotUnixMs` range, ascending)?

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
- Slice (S1 | S2 | cross-slice)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
