---
created: 2026-05-18
type: audit-followup
parent: wave6-S1-S2-slice-audit
status: applied
---

# Wave 6 — S1+S2 slice-audit followup (APPROVE WITH CHANGES)

S1+S2 slice-audit verdict on commit `8fcfeb6`: APPROVE WITH
CHANGES (0 C / 1 H / 1 M / 1 L). All three findings closed
in this commit.

---

## H1 — SourceFirstDropout missing window-span guard

**Verified:** `P2pAlertEvaluator.EvaluateSourceFirstDropouts`
only required `WindowSnapshots.Count >= 2` and
`newest.SnapshotUnixMs > oldest.SnapshotUnixMs`. Two
snapshots 30 s apart in a sparsely-sampled store would
fire a "1-hour dropout" alert despite covering only 30 s.

**Revision applied:** Added a strict span guard:

```csharp
if (newest.SnapshotUnixMs - oldest.SnapshotUnixMs
    < input.Config.SourceFirstDropoutWindowMs)
    yield break;
```

The boundary is inclusive (`<`, not `<=`) so an exactly-
windowMs span still fires. New pins:

- `Rule4_SourceFirstDropout_ShortSpanWindow_DoesNotFire` —
  30 s span vs 1 h window → no alert.
- `Rule4_SourceFirstDropout_ExactlyWindowSpan_Fires` —
  exact-windowMs span fires.

The existing `Rule4_SourceFirstDropout_FiresWhenOneSourceWentSilentButOthersDidNot`
test already covered a 1 h span and continues to pass
unchanged.

---

## M1 — Append-only invariant not enforced by Raven repo

**Verified:** `RavenAlertEventRepository.SaveAsync` called
`session.StoreAsync(alertEvent, alertEvent.Id)` without a
duplicate-id guard. Raven's default semantics overwrite an
existing document with the same id, silently violating the
master.md Core Rule §3 append-only invariant.

**Revision applied:**

1. `RavenAlertEventRepository.SaveAsync` now performs
   `session.Advanced.ExistsAsync(id)` before storing; on a
   collision it throws
   `InvalidOperationException("P2pAlertEvent id collision: …")`.
   The store call also passes `changeVector: string.Empty`
   so a concurrent write between the exists-check and the
   commit also fails (Raven's empty-change-vector semantics
   require the doc to NOT exist).
2. `IAlertEventRepository.SaveAsync` xmldoc updated to
   declare the throw-on-duplicate contract.
3. New integration test fixture
   `RavenAlertEventRepositoryTests` (extends
   `RavenTestDriver`, follows the
   `BlockHeaderStoreTests` pattern) with two pins:
   - `SaveAsync_DuplicateId_ThrowsAndDoesNotOverwrite` —
     stores an alert, attempts a second save with the same
     id and mutated payload, asserts `InvalidOperationException`
     AND that the original document is intact.
   - `SaveAsync_FreshId_PersistsSuccessfully` — happy path.

Both tests are `SkippableFact` with
`Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8))`
mirroring the existing Raven embedded test conventions
(the local dev environment lacks .NET 8 for the embedded
server; CI runs them).

---

## L1 — Telemetry fault isolation untested at unit level

**Verified:** `PeerManager.EvictByRotationPlanner` had the
correct `try/catch` per peer at code level, but no test
proved a throwing `Telemetry.Snapshot()` was isolated.

**Revision applied:**

1. New public static helper
   `PeerRotationPlanner.ScoreActivePeers(peers, policy, onFault)`
   — pure function that iterates `(Key, IPeerTelemetrySink)`
   pairs, swallows per-peer faults via the `onFault`
   callback, and returns successfully-scored peers.
2. `PeerManager.EvictByRotationPlanner` refactored to
   delegate to the helper, removing the inline try/catch
   loop. The fault-handling path now goes through testable
   code.
3. New pin
   `ScoreActivePeers_TelemetrySinkThrows_OtherPeersStillScored`
   in `PeerRotationPlannerTests` — three peers, the middle
   one's sink throws on `Snapshot()`. Asserts the helper
   returns 2 scored peers (skipping the bad one) and
   invokes `onFault` exactly once for the bad key.

Test counts:

- `Dxs.Bsv.Tests`: 250/250 green (was 189 before S0; 29
  P2p/Pool tests now, +1 fault-isolation pin).
- `Dxs.Consigliere.Tests` P2p+Metrics: 21/21 green (S2
  evaluator+poller; +2 evaluator window-span pins).
- Raven integration: 2 skipped locally (matches the
  pre-existing Raven embedded skip pattern; CI runs).

Slice gates S1 + S2 cleared; S3 (admin endpoint) opens.
