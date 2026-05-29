# thin-node S3 — slice-audit prompt

Audit target: S3 (all realtime sources concurrent; cold-start honest).
Diff: `8d65a1b..e29f572`.

Read `master.md` Product Decision 2 + Core Rules 3-4, `slices.md` §S3.

## What landed
- `RealtimeIngestBackgroundTask` rewritten: was "run only the resolved
  PRIMARY's runner" (a switch on `route.PrimarySource`) → now
  `RunEnabledRunnersAsync` `Task.WhenAll`s every ENABLED external realtime
  runner. New `internal static ResolveEnabledExternalRunners(config)`
  derives the set from effective config (Enabled && EnabledCapabilities
  contains realtime_ingest); p2p + node excluded (own always-on wiring).
- Recycle signature now keys off the enabled-runner SET + each runner's
  connection settings (was: single primary's settings).
- New `RealtimeIngestRunnerSelectionTests` (5).

## Focus
1. **No runner silenced (the core bug).** With p2p primary (new default),
   confirm Bitails AND JungleBus runners both run. Pre-S3, p2p primary
   would have run NOTHING in this task (p2p has no runner here) → ingest
   dead except the P2P observer. Confirm the fix.
2. **Cold-start honest (Core Rule 4).** Runner selection takes NO
   P2P-health input — a 0-peer pool cannot reduce the external runner set.
   Verify there's no hidden p2p-health gate.
3. **P2P observer still always-on.** `P2pMempoolIngestRunner` runs via its
   own `BsvP2pSetup` hosted service (gated on `BsvP2pConfig.Enabled`),
   independent of this task. Confirm S3 didn't touch it and it isn't
   double-scheduled.
4. **Primary still the attribution anchor.** Confirm nothing that legit
   depends on the resolved primary broke — `AppInitBackgroundTask`/
   `RealtimeBootstrapPlanner` (node ZMQ bootstrap + mempool-scan-on-start),
   `OpsController` "active provider", block-backfill routing. S3 says it
   left these untouched and still logs the primary.
5. **Recycle correctness.** Enabling/disabling a runner or editing its
   credentials must recycle; confirm the new signature covers the set.
6. **No journal/dedup change.** Write contract + `SeenBySources` dedup
   untouched (concurrent appends already safe per Wave 2).

## Validation evidence
- `dotnet build … -c Release` clean.
- Realtime tests 30/30 (5 new + 25 existing). Multi-source SeenBySources
  accumulation proven by existing `SeenBySourcesProjectionTests`
  (RavenTestDriver — skipped in this env).

## Verdict + finding format
Verdict first; findings `C*|H*|M*|L*` with file:line, why, fix.
