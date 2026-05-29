# wizard-enabled-p2p-runtime-toggle — closeout

## Result

Completing the first-run wizard now turns the BSV thin node ON, live, with no
restart. The enable state is persisted to a DB-authoritative-after-seed
runtime-settings document (seeded from `BsvP2pConfig.Enabled`), and the P2P
hosted service is a supervisor that starts/stops the peer pool on that flag,
watching the document via the RavenDB Changes API. The mempool observer
follows the pool automatically. Environments that never run the wizard (CI/E2E,
pre-wizard) keep the config seed (false) and never touch the network.

## Key files

- `src/Dxs.Consigliere/Data/Models/Runtime/OperatorRuntimeSettingsDocument.cs`
  — operator runtime toggles (P2pEnabled), no secrets.
- `src/Dxs.Consigliere/Data/Runtime/OperatorRuntimeSettings{Store,Service}.cs`
  — seed-from-config, DB-authoritative.
- `src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs` — enables P2P on
  completion.
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs` — supervisor +
  Changes-API live toggle.
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` —
  pool-readiness-driven observer.

## Behavioral summary

- Fresh turnkey install: complete the wizard → thin node comes up live (pool +
  observer), no restart, no appsettings edit.
- The enable decision lives in the DB (survives restarts) and is the nucleus
  of a future "runtime settings in the DB" model — added narrowly, no big-bang
  config migration, secrets untouched.
- Toggle is safe both ways: routing/rawTx/realtime/broadcast already tolerate
  the subsystem being on or off; start/stop is idempotent + serialized.

## Residuals (honest)

- **Live pool start/stop is operator-run / integration, evidence-pending** —
  unit coverage is the pure start/stop decision + idempotency + CI-safe-off,
  not the real peer network bring-up.
- **Changes-connection drop** silently disables the live toggle until restart
  (boot-time state still applies) — follow-up candidate.
- **No admin toggle endpoint/UI** and **no migration of other settings** —
  intentionally out of scope; the document is the seed for the latter.
- `BsvP2pConfig.Enabled` base default stays false; the wizard (DB) turns it on.

## Delivery

| slice | commit | status |
|---|---|---|
| S1 runtime-settings doc + wizard write | `34ecce0` | done |
| S2 supervisor + live toggle | `01ac6f3` | done |
