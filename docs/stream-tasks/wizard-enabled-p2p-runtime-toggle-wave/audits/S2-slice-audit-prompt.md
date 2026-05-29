# wizard-p2p S2 — slice-audit prompt

Audit target: S2 (P2P hosted-service supervisor + live toggle).
Diff: `34ecce0..01ac6f3`.

Read `master.md` (Product Decision 3, Core Rules 2-4), `slices.md` §S2.
This is a **lifecycle refactor of the P2P subsystem** — audit concurrency,
teardown, and CI-safety carefully.

## What landed
- `BsvP2pHostedService` → supervisor. No more `if(!Enabled) return` at start.
  StartAsync: subscribe to the runtime-settings doc via the RavenDB Changes
  API, then `ReconcileAsync`. Effective-enabled = `GetP2pEnabledAsync` (DB ??
  config seed). `StartPoolAsync`/`StopPoolAsync` extracted.
  `DecideAction(enabled, running)` pure (Start/Stop/None).
- Reconcile serialized by `SemaphoreSlim` with an `acquired` flag (release iff
  acquired); state re-read inside the lock. Changes-subscribe in try/catch
  (degrade to logged warning + boot state). StopAsync disposes the sub,
  cancels lifetime, then acquires the gate before teardown.
- `Network != mainnet` → logs error + pool stays down (was: throw at startup).
- `P2pMempoolIngestRunner` pool-readiness-driven: always starts its loop but
  is inert until `BsvP2pHealth.Bound`; lazily loads the watchlist on the first
  bound tick; clears stale session wirings when the pool drops.

## Focus (be adversarial)
1. **Semaphore correctness.** Verify no path releases the gate it didn't
   acquire, and no path leaks it (the `acquired` flag in ReconcileAsync;
   StopAsync acquires with `CancellationToken.None`). Can a Changes-callback
   reconcile and StopAsync deadlock or double-stop? Trace the lifetime-cancel
   → in-flight reconcile bails → StopAsync acquires → teardown.
2. **Idempotency / no double-start.** Concurrent change callbacks: DecideAction
   + the gate must ensure exactly one pool instance. Confirm `_poolRunning`
   transitions only inside the lock. Is there a window where two StartPoolAsync
   run? (Should be impossible — gate serializes, state re-read inside.)
3. **Teardown completeness.** `StopPoolAsync` disposes the PeerManager, nulls
   `_store`, unbinds health. On a stop→start cycle, are there leaked sessions,
   timers, or dispatcher subscriptions? `DisposeAsync` disposes sub + manager +
   cts + gate.
4. **CI/E2E safe (Core Rule 4).** With config seed false + no DB override, the
   pool stays down and NO peer connection is attempted. The observer loop runs
   but is inert (not Bound) and does no Raven watchlist load. Confirm.
5. **Observer coupling.** The observer now keys off `BsvP2pHealth.Bound`, not a
   flag. When the pool comes up mid-run, does the 5s reconcile reliably pick up
   the new Ready peers + load the watchlist before wiring? When the pool drops,
   are stale wirings cleared (no callback into disposed sessions)?
6. **Changes-API liveness.** `.ForDocument(id).Where(Put|Delete).Subscribe`.
   Does a Delete (revert to seed) reconcile correctly? Is the subscription
   re-established if the Raven changes connection drops mid-run? (Likely NOT —
   note as a residual if so: a dropped changes connection silently disables the
   live toggle until restart.)
7. **mainnet-guard behaviour change.** Logging an error instead of throwing:
   acceptable (a misconfigured subsystem shouldn't crash the host) or should it
   be louder/fail-fast? Operator-judgment.

## Validation evidence
- `dotnet build … -c Release` clean.
- `BsvP2pSupervisorDecisionTests` 4/4 (Start/Stop/None incl. idempotent +
  CI-safe-off); `InboundConfigStubTests` 4/4 (new ctor); observer 4/4;
  `BsvP2pSetupDiResolutionTests` 6/6 (the W2 A2 C1 ctor-drift guard — the new
  cross-zone dep `IOperatorRuntimeSettingsService` was registered).
- Full suite: only pre-existing env failures (BroadcastDi → IAuditLogger
  unregistered; TransactionStore → RavenTestDriver; one Raven-parallel flake).
- **Live pool start/stop with real peers is operator-run / integration —
  evidence-pending** (unit coverage is the pure decision + idempotency, not the
  network bring-up).

## Verdict + finding format
Verdict first; findings `C*|H*|M*|L*` with file:line, why, fix.
