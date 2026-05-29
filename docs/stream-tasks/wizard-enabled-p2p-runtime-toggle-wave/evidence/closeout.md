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

## Live end-to-end — CONFIRMED (2026-05-29)

Operator ran the turnkey Local stack
(`docker compose -f compose.local.yml -f compose.local-build.yml up -d --build`)
with `BsvP2pConfig.Enabled` at its default (false), completed the first-run
wizard, and the thin node came up **live, no restart**. The admin P2P Pool
screen (`/api/admin/p2p/{health,peers}`) showed, immediately after wizard
completion:

- Pool **6/8** (supervisor brought the pool up and is converging to the
  target), `/24 diversity 6`.
- 6 config-seed peers handshaked (`Source: Config`, composite 100, e.g.
  `135.181.137.155:8333` ok 44/0).
- addr-gossip discovery active (`185.152.150.197:8333`, `Source: AddrGossip`).
- 1874 peers discovered · 7 successful · 52 failed — expected cold-start
  acceptance ratio (UA filtering, per project memory), not a defect.

This confirms the full chain `wizard Complete → P2pEnabled=true (DB) →
RavenDB Changes API → supervisor → pool live`, with no appsettings edit and
no restart — the wave's definition of done.

## Residuals (honest)

- **Soak / sustained pool health** beyond first-minute bring-up is still
  operator-run (the >24h Gate-2 soak is a separate track).
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
