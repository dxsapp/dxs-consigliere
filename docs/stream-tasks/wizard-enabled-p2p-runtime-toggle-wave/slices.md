# Slices — wizard-enabled P2P + runtime toggle

Source of truth for decisions/ledger: `master.md`. Co-author trailer on
every commit: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

On ambiguity, make the smallest safe judgment call that preserves
behaviour, document it in `audits/A1.md`, and continue. Do NOT halt unless
a truly blocking contradiction emerges.

## Verified anchors (read before editing)

- `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` — `Enabled` (default false),
  the config seed.
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs` — `StartAsync`
  reads `_config.Enabled` once (L62) and returns early when false; builds
  `InMemoryPeerStore` + `PeerDiscovery` + `PeerManager`, binds `BsvP2pHealth`
  (`_manager`, `_store` fields). This is the thing S2 turns into a supervisor.
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` — `Bind/Unbind`,
  `Bound`, `ActiveSessions`. The "is the pool up" signal consumers already use.
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` — the mempool
  observer (separate hosted service). S2 must confirm whether it gates on
  `BsvP2pConfig.Enabled` or is always-on and keys off peer sessions.
- `src/Dxs.Consigliere/Data/Models/Runtime/SetupBootstrapDocument.cs` — the
  `AuditableEntity` pattern (AllKeys/UpdateableKeys/ToEntries/GetId,
  `DocumentId = "operator/runtime/..."`) to copy for the new doc.
- `src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs` — `CompleteAsync`
  (L89+) already persists `SetupBootstrapDocument` via `setupStore.SaveAsync`;
  add the runtime-settings write alongside it.
- `src/Dxs.Consigliere/BackgroundTasks/StasAttributesChangeObserverTask.cs`
  — the RavenDB Changes-API hot-reload exemplar:
  `db.Changes().ForDocumentsInCollection(...).Where(Put/Delete).Subscribe(...)`.
  Use `.ForDocument(id)` for the single settings doc.
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` — DI wiring for the P2P zone.

---

## S1 — runtime-settings doc + seed + wizard write

**Intent.** Introduce a DB-authoritative-after-seed store for operator
runtime toggles, starting with `P2pEnabled`, and have the wizard turn the
thin node on.

**Owned paths.**
- NEW `src/Dxs.Consigliere/Data/Models/Runtime/OperatorRuntimeSettingsDocument.cs`
  (`AuditableEntity`, `DocumentId = "operator/runtime/operator-settings"`,
  `bool? P2pEnabled` — null = not overridden).
- NEW store/service, e.g. `src/Dxs.Consigliere/Data/Runtime/OperatorRuntimeSettingsService.cs`
  (+ interface) with `Task<bool> GetP2pEnabledAsync(ct)` =
  `doc?.P2pEnabled ?? bsvP2pConfig.Enabled`, and
  `Task SetP2pEnabledAsync(bool, updatedBy, ct)`. Seed-on-first-read may
  persist a seeded doc (like `AdminProviderConfigService.GetPersisted…` does)
  or just compute the fallback — choose the simpler safe option, document it.
- `src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs` —
  `CompleteAsync` calls `SetP2pEnabledAsync(true, updatedBy, ct)` after the
  provider config + bootstrap writes.
- DI registration (likely `BsvP2pSetup` or the data-runtime setup).
- NEW unit tests.

**What not to do.** Do NOT touch `BsvP2pHostedService` (S2). Do NOT move
any secret into the new doc. Do NOT change `BsvP2pConfig`'s default. Do NOT
generalise to other settings.

**Validation.** `dotnet build … -c Release`. Unit: no doc → `GetP2pEnabledAsync`
returns the config seed (false by default); doc with `P2pEnabled=true` →
returns true; `SetP2pEnabledAsync` persists; wizard `CompleteAsync` writes
`P2pEnabled=true` (assert via a fake store).

**Completion signal.** The setting round-trips through the DB and the wizard
turns it on; build + tests green.

---

## S2 — P2P hosted-service supervisor + live toggle

**Intent.** Make the thin node start/stop at runtime on the effective
enable state, watching the settings doc — no restart.

**Owned paths.**
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs` — supervisor refactor.
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` — wiring if needed.
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` — ONLY if it
  must observe the toggle (see investigation).
- NEW unit/integration test(s).

**Exact task.**
1. Extract the current `StartAsync` body (build store/discovery/manager,
   bind health) into `StartPoolAsync(ct)` and a clean `StopPoolAsync()`
   (unbind health, dispose manager/sessions/timers). Both idempotent
   (guard on a running flag).
2. `StartAsync` no longer early-returns on disabled. It: reads effective
   enabled = `await runtimeSettings.GetP2pEnabledAsync()` (DB ?? config
   seed); if true → `StartPoolAsync`; subscribes to the RavenDB Changes API
   on `OperatorRuntimeSettingsDocument.DocumentId`
   (`.ForDocument(id).Where(Put).Subscribe(...)`); on change → re-read
   effective enabled → `StartPoolAsync`/`StopPoolAsync` as needed.
3. `StopAsync` → `StopPoolAsync` + dispose the Changes subscription.
4. **Observer activation.** Investigate `P2pMempoolIngestRunner`: if it
   gates on `BsvP2pConfig.Enabled` at startup, make it react to the same
   effective-enabled (observe pool readiness `BsvP2pHealth.Bound`, or share
   the toggle). If it is already always-on and simply consumes peer
   sessions as they appear, no change — document which it is.
5. Keep `Network != mainnet` guard + the inbound-stub warning behaviour.

**What not to do.** Do NOT change the routing/rawTx/realtime consumers
(they already tolerate on/off). Do NOT add an admin endpoint (out of scope).
Do NOT enable P2P in base appsettings.

**Validation.** `dotnet build … -c Release`. Unit/integration: supervisor
starts the pool when effective-enabled is true at boot; flips up on a
`P2pEnabled false→true` change and down on `true→false` (idempotent —
double-start / double-stop safe); with no DB override + config seed false,
the pool stays down (CI/E2E safe — assert no peer connect attempt). If a
full peer-pool harness is too heavy, unit-test the supervisor's start/stop
decision + idempotency and state what was tested.
Note: `RavenTestDriver` tests fail in this env (no Raven server) — pre-existing.

**Completion signal.** Flipping `P2pEnabled` in the DB starts/stops the
thin node live; clean teardown; CI/E2E stay off.

---

## Dependency order
S1 → S2 (S2 reads the setting S1 defines). Sequential — same backend, small.
Run S1 local, then S2 (or one agent per slice). No parallel zones here.

## Validation matrix
| slice | backend | proof |
|---|---|---|
| S1 | `dotnet build … -c Release` | settings seed/override round-trip + wizard write unit |
| S2 | `dotnet build … -c Release` | supervisor start/stop + idempotency + CI-safe-off unit |

End-to-end (operator-run, evidence-pending): fresh turnkey Local stack with
`BsvP2pConfig.Enabled=false` → complete wizard → thin node comes up live
(BsvP2pHealth.Bound true, peers connecting) with no restart.

## Closeout
- Ledger rows done / not_opened; hashes in master Delivery Notes (separate
  backfill commit).
- `audits/A1.md` + `evidence/closeout.md`; per-slice audit prompts.
