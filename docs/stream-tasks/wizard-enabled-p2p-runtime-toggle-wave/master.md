---
created: 2026-05-29
type: wave
parent: docs/stream-tasks/consigliere-thin-node-observer-program/
related: docs/stream-tasks/thin-node-primary-source-wave/evidence/closeout.md (p2p is the routed default; subsystem still off by default);
         docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/ (turnkey local mode + restart-free wizard→ingest)
status: approved (planning only — no slices executed yet)
---

# Wizard-enabled P2P + runtime toggle

The `thin-node-primary-source-wave` made `p2p` the routed default primary
for realtime + rawTx. But the P2P **subsystem** itself
(`BsvP2pHostedService`) still reads `BsvP2pConfig.Enabled` ONCE at
`StartAsync` and returns early when false (the Gate-2 disabled-by-default
master switch). So on a fresh turnkey install the thin node never actually
runs until an operator edits appsettings and restarts — exactly the manual
step the turnkey promise is meant to remove. Today routing lists p2p
primary, every fetch finds no peers, and the system silently rides the
external fallbacks.

This wave closes that: completing the first-run wizard turns the thin node
**on**, live, with no restart — mirroring how wave-A4 made
wizard→provider-config restart-free. It does so by introducing the nucleus
of the operator's broader goal — **runtime settings seeded from config,
DB-authoritative after first seed** — scoped to exactly what's needed now,
not a big-bang config migration.

## Goal

A user who completes the wizard on a fresh turnkey install gets a running
thin node (peer pool + mempool observer) within seconds, no restart, no
appsettings edit. The enable state lives in the DB (seeded from config),
so it survives restarts and can later be toggled by an operator.

Business outcome: the "wizard → working P2P-first product" path is real,
not gated on a hand-edited master switch.

## Product Decision

1. **Completing the wizard enables the thin node.** `SetupComplete`
   persists `P2pEnabled = true`. The thin node is the product's default
   ingest (per the thin-node-primary-source wave), so finishing setup
   turns it on. (A future admin toggle can flip it off; out of scope here.)
2. **Config is the SEED; the DB is authoritative after first seed.** A new
   `OperatorRuntimeSettingsDocument` (RavenDB) is seeded from
   `BsvP2pConfig.Enabled` on first read, then becomes the source of truth.
   appsettings `Consigliere:Broadcast:P2p:Enabled` keeps its meaning as the
   first-run default (and the only signal in environments that never run
   the wizard, e.g. CI/E2E — which stay OFF).
3. **Runtime start/stop, not restart.** `BsvP2pHostedService` becomes a
   supervisor: it always starts, computes effective-enabled (DB ?? config
   seed), brings the pool up when enabled and tears it down when disabled,
   and watches the settings document via the RavenDB Changes API (the same
   primitive `StasAttributesChangeObserverTask` / `RavenWatchlistLoader`
   use) so a wizard write flips it live.
4. **Scope discipline — NOT a config-to-DB migration.** Only the P2P
   enable flag moves into the runtime-settings doc in this wave. The
   document is designed to grow (the nucleus of a later "runtime settings
   store" program), but secrets stay in the secrets file (wave-A2/A3
   decision) and bootstrap/infra config (Raven URL, hosting, env) stays in
   appsettings — neither moves here.

## Scope

In scope:
- `OperatorRuntimeSettingsDocument` (RavenDB) + a small store/service with
  seed-from-`BsvP2pConfig` semantics and a typed `P2pEnabled` accessor.
- Wizard `CompleteAsync` writes `P2pEnabled = true`.
- `BsvP2pHostedService` refactor: supervisor lifecycle (extract
  start-pool / stop-pool, idempotent), effective-enabled = DB ?? config,
  Changes-API watch → live start/stop.
- Ensure the mempool **observer** (`P2pMempoolIngestRunner`) is active when
  the pool comes up at runtime (either it is already always-on and keys off
  peer sessions, or it must observe the same toggle — to be resolved in S2).

Out of scope (rationale):
- **Admin endpoint / UI to toggle P2P** post-wizard — useful, but the
  wizard-write covers the stated goal; deferred to a follow-up.
- **Migrating other settings into the DB** — the document is the seed for
  that, but generalising (custom `IConfigurationProvider`, `IOptionsMonitor`
  reload, per-consumer hot-reload audit) is a separate program.
- **Moving secrets back into the DB** — wave-A2/A3 deliberately put them in
  the secrets file; do not regress.
- **Enabling P2P globally in base appsettings** — the default stays OFF;
  the wizard (DB) is what turns it on for real deployments.

## Core Rules

1. **DB authoritative after seed; config is the first-run default.** Read
   path: `effectiveEnabled = dbOverride ?? configSeed`. Never let a stale
   config value override an explicit DB decision.
2. **Restart-free.** Flipping `P2pEnabled` must start/stop the pool at
   runtime. No code path may require a process restart to take effect.
3. **Safe both ways.** Consumers already tolerate the subsystem being off
   (rawTx → null → fallback; routing lists p2p but fetches miss). Bringing
   it up or down mid-run must not throw in routing / rawTx / realtime /
   broadcast. Start/stop must be idempotent and clean (no leaked peer
   sessions, dispatchers, or timers).
4. **CI/E2E stay off.** Environments that never run the wizard (no DB
   override) fall back to the config seed, which stays `false`. The
   contract-test/E2E backend must not start connecting to mainnet peers.
5. **No secrets in the new document.** `secrets-lint` green; the runtime
   settings doc holds operational toggles only.
6. **Hash-backfill discipline** (carried): Delivery Notes hashes in a
   SEPARATE commit, never `--amend`.

## Ownership Zones

- `runtime-settings` — NEW `OperatorRuntimeSettingsDocument` +
  store/service (`Data/Models/Runtime/`, `Data/Runtime/`),
  `SetupWizardService.CompleteAsync` (the write). (S1)
- `p2p-lifecycle` — `BsvP2pHostedService` (supervisor refactor),
  `BsvP2pSetup` wiring, and the observer activation
  (`P2pMempoolIngestRunner`) only as needed. (S2)

## Wave Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S1 | `runtime-settings` | todo | — | `dotnet build … -c Release`; unit: settings service seeds `P2pEnabled` from `BsvP2pConfig.Enabled` when no doc exists, returns the DB value when present; wizard `CompleteAsync` persists `P2pEnabled=true` | `OperatorRuntimeSettingsDocument` + store/service with seed-from-config + typed `P2pEnabled`; wizard completion writes it; no secret in the doc | `audits/S1-slice-audit-prompt.md` |
| S2 | `p2p-lifecycle` | todo | S1 | `dotnet build … -c Release`; unit/integration: supervisor brings the pool up when effective-enabled flips true and tears it down on false (idempotent); Changes-API watch triggers it; with no DB override + config seed false, pool stays down (CI/E2E safe) | `BsvP2pHostedService` is a runtime supervisor (no early-return gate); effective-enabled = DB ?? config; live start/stop via Changes-API watch; observer active when pool is up; restart-free proven | `audits/S2-slice-audit-prompt.md` |

## Definition of Done

- Completing the wizard on a fresh install starts the thin node live (pool
  + observer) with no restart; `P2pEnabled=true` persisted in the DB.
- With no DB override, effective-enabled = config seed (false by default) —
  CI/E2E and un-wizarded deployments do not connect to peers.
- Start/stop is idempotent and clean; routing / rawTx / realtime /
  broadcast never throw on a mid-run toggle.
- `dotnet build Dxs.Consigliere.sln -c Release` + backend unit tests green
  (modulo pre-existing RavenTestDriver env failures); `secrets-lint` 0.
- `audits/A1.md` + `evidence/closeout.md` written.

## Delivery Notes

| slice | commit | summary |
|---|---|---|
| S1 | _pending_ | _runtime-settings doc + seed + wizard writes P2pEnabled_ |
| S2 | _pending_ | _P2P hosted-service supervisor + live toggle via Changes API_ |
| Audit folds | _pending_ | _per-slice findings folded_ |
