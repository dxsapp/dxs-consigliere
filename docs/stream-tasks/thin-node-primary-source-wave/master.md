---
created: 2026-05-29
type: wave
parent: docs/stream-tasks/consigliere-thin-node-observer-program/
related: docs/stream-tasks/bsv-mempool-observer-wave/ (Wave 2 — P2P observer landed);
         docs/stream-tasks/admin-realtime-source-policy-wave/ (capability-routing model);
         docs/stream-tasks/observation-source-metrics-wave/ (per-source metrics base);
         docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/evidence/closeout.md (turnkey local mode)
status: done (S1+S2+S3+S5 shipped; S4 not_opened — already delivered by Wave 4)
---

# Thin-node primary source — routed realtime + rawTx

The `consigliere-thin-node-observer-program` already landed the P2P
**observer** (Wave 2): `inv(MSG_TX)` → rate-limited `getdata` →
watchlist match → journal append with `SeenBySources += "p2p"`, and a
fully working P2P **broadcaster** (`TxRelayCoordinator`). But the
provider-selection model the operator actually sees — the first-run
wizard and the capability-routing layer — still treats **realtime** and
**rawTx** as a choice between *external* providers
(bitails / junglebus / whatsonchain). The thin node is invisible to
that routing: it runs in parallel as a hidden observer, never as the
selected primary.

This wave closes that gap: make the thin node a **first-class, routable
provider** named `p2p`, the **default primary** for realtime + rawTx,
with external providers demoted to fallback and JungleBus scoped to
historical block backfill. It is the program's "P2P-first ingest" end
state expressed through the routing + setup surface, not a new
subsystem.

## Goal

A fresh operator who completes the wizard gets the thin node as the
primary realtime + rawTx source out of the box; Bitails and JungleBus
keep running as redundant realtime observers (so a cold-start P2P pool
never stalls ingest), and the admin UI shows per-source "who saw it
first" latency. rawTx resolves from P2P first and auto-falls-back to
external providers for confirmed/historical txids the peers won't serve.

Business outcome: Consigliere indexes BSV straight from the P2P network
by default, with external HTTP providers as measurable redundancy — the
self-sufficient-indexer promise of the program, reachable from the
turnkey local mode shipped in wave-A4.

## Product Decision

Operator-confirmed (this wave):

1. **rawTx = `p2p` primary + external auto-fallback.** P2P `getdata`
   reliably serves only mempool + recent tx; peers do NOT serve
   arbitrary confirmed/historical txids (those live in blocks). So the
   P2P rawTx fetcher returns bytes on a hit and **null on miss/timeout**,
   and the existing `RawTransactionFetchService` primary→fallback loop
   then tries junglebus / whatsonchain. Nothing is lost; the physics of
   P2P is reflected honestly.
2. **Realtime = all sources run, always; track who's fastest.** When
   `p2p` is the realtime primary, the Bitails and JungleBus realtime
   runners do NOT stop — they keep observing in parallel (the journal is
   already source-aware and dedupes by txid). "Primary" is the canonical
   attribution + state-transition source, not an exclusive switch. A
   per-source **first-seen latency** metric records which source saw
   each txid first and how far the others lagged.
3. **Thin node is the hard default for a fresh install.** The wizard
   recommends `p2p` for realtime + rawTx with no operator action. The
   cold-start peer-acceptance risk (UA filtering; Gate-2 soak not fully
   green) is mitigated by Decision 2: external runners are always on, so
   a fresh host still indexes from minute one and P2P catches up as its
   pool warms.
4. **JungleBus = historical block backfill.** Recommended rawtx/realtime
   roles move off junglebus; its block-subscription / backfill role
   (`BlockBackfill`) is unchanged. The wizard's "Block sync" step stays.
5. **Provider name is `p2p`, distinct from `node`.** `node` already
   means a full bitcoin-sv RPC node (`SourceCapabilityRouting.NodeProvider`).
   The thin node registers as a separate provider `p2p` (matching the
   existing `TxObservationSource.P2p` tag and `SeenBySources` value).

## Scope

In scope:
- Register the thin node as a routable provider `p2p` via a new
  `IExternalChainProviderDiagnostics` descriptor
  (`Capabilities = [RealtimeIngest, RawTxFetch]`), health bridged from
  `BsvP2pHealth`. Gate its routability on `BsvP2pConfig.Enabled`.
- A P2P rawTx fetcher (`getdata(MSG_TX,txid)` → first `tx` frame, with
  timeout, null on miss) wired as the `p2p` case in
  `RawTransactionFetchService`; external auto-fallback via the existing
  loop.
- Run Bitails + JungleBus realtime runners concurrently (not just the
  resolved primary) alongside the always-on P2P observer.
- Per-source first-seen latency metric + admin surface.
- Recommendation + candidate defaults moved to `p2p`; wizard Providers
  step remodeled (p2p primary, external = fallback, JungleBus = block
  sync); DTO + frontend updated.

Out of scope (rationale — do NOT pull in):
- **Inbound P2P listener** — deferred in the program (Gate 4 / Wave 6
  stub). Outbound pool is sufficient for observer + rawTx.
- **General historical tx archive over P2P** — peers don't serve it;
  that's exactly why external rawTx fallback stays (Decision 1).
- **Block backfill over P2P** — JungleBus keeps `BlockBackfill`
  (Decision 4). No P2P block-sync in this wave.
- **Removing the `node` (RPC) provider** — orthogonal cleanup; leave it.
- **Multi-tenant watchlists / Postgres / bloom filters** — program-level
  out-of-scope, unchanged.

## Core Rules

1. **`p2p` is descriptor-registered, not hard-coded.** It joins the
   catalog like Bitails/JungleBus/WoC (an `IExternalChainProviderDiagnostics`),
   so routing, health, and admin surfaces pick it up uniformly. The only
   special-case is the `BsvP2pConfig.Enabled` routability gate in
   `SourceCapabilityRouting.CanServe` (mirroring the existing `node`
   gate).
2. **P2P rawTx miss is not an error.** A `getdata` timeout / `notfound`
   returns `null`, never throws — so the fetch-service fallback loop
   proceeds. Only a genuine transport fault logs a warning (same as the
   other providers).
3. **No realtime source is silenced when another is primary.** Making
   `p2p` primary must NOT disable the Bitails/JungleBus realtime runners.
   If the current `RealtimeIngestBackgroundTask` runs only the resolved
   primary, it changes to run external runners concurrently. (Decision 2.)
4. **Honest cold-start.** Fresh-install ingest must not depend solely on
   the P2P pool warming up. Validate that with `p2p` primary AND zero
   connected peers, the external runners still feed the journal.
5. **No back-compat shims.** Both ends in-repo. The wizard DTO changes in
   place; no legacy field kept beside it.
6. **Prod-compile gates.** Frontend: `pnpm verify` (ends with
   `contracts:check`) + `pnpm test:contract` 24/24. Backend:
   `dotnet build Dxs.Consigliere.sln -c Release`. `secrets-lint` green.
7. **Hash-backfill discipline (carried from A3/A4).** Delivery Notes
   hashes in a SEPARATE follow-up commit, never `--amend`.

## Ownership Zones

- `provider-routing` — `SourceCapabilityRouting.cs`,
  `RawTransactionFetchService.cs`, new `P2pProviderDiagnostics`
  (+ `IP2pRawTransactionClient` impl), `ExternalChainProviderName.cs`
  (add `P2p`), `ExternalChainAdaptersSetup.cs` (catalog DI),
  `AdminProviderConfigService.cs` (recommendation constants + candidate
  arrays). (S1, S2)
- `realtime-orchestration` — `RealtimeIngestBackgroundTask.cs` + the
  per-provider realtime runners; P2P observer wiring
  (`BsvP2pSetup`/`P2pMempoolIngestRunner`) only where it confirms
  always-on. (S3)
- `source-metrics` — `SourceMetricsSnapshot.cs`, the first-seen
  attribution writer, `AdminMetricsController.cs`, the admin-ui metrics
  screen. (S4)
- `setup-wizard` — `SetupCompleteRequest.cs` / setup DTOs,
  `SetupWizardService.cs`, `Step2Providers.tsx`, `setup-wizard.store.ts`,
  generated types. Consumes the backend defaults S1 sets; does NOT edit
  `AdminProviderConfigService`. (S5)

## Wave Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S1 | `provider-routing` | **done** | — | `dotnet build … -c Release`; unit: routing resolves `p2p` as primary for RealtimeIngest + RawTxFetch when `BsvP2pConfig.Enabled`, and is skipped when disabled | `p2p` registered as a catalog descriptor (`[RealtimeIngest, RawTxFetch]`); `ExternalChainProviderName.P2p`; recommendation constants + candidate arrays include `p2p`; `CanServe` gates `p2p` on `BsvP2pConfig.Enabled` | `audits/S1-slice-audit-prompt.md` |
| S2 | `provider-routing` | **done** | S1 | unit: `p2p` hit returns raw bytes; `p2p` miss/timeout → null → fetch-service falls through to junglebus/whatsonchain; transport fault logs + continues | `RawTransactionFetchService` has a `p2p` case that issues `getdata` → awaits first `tx` frame (timeout, null-on-miss) via the existing per-session dispatcher; external auto-fallback proven | `audits/S2-slice-audit-prompt.md` |
| S3 | `realtime-orchestration` | **done** | S1 | unit/integration: with `p2p` primary, Bitails + JungleBus realtime runners both active concurrently with the P2P observer; with 0 P2P peers, external runners still append to the journal | Realtime "primary" no longer silences other runners; all configured realtime sources observe in parallel; cold-start ingest proven independent of P2P pool | `audits/S3-slice-audit-prompt.md` |
| S4 | `source-metrics` | **not_opened** | S3 | already-delivered: verified `SourceVisibilityTracker` + `/api/admin/metrics/sources` + UI sparklines exist | **Already delivered by the program's Wave 4 (`observation-source-metrics-wave`).** `TxObservationJournalWriter` calls `RecordObservation(txId, message.Source, …)` for EVERY source, so `p2p` first-seen wins + lag buckets are tracked source-agnostically; the admin metrics screen renders per-source first-seen sparklines (`source-metrics.store.test.ts` asserts `firstSeenSeries("p2p")`). No new code needed. | `audits/S4-slice-audit-prompt.md` |
| S5 | `setup-wizard` | **done** | S1 | `pnpm verify` + `pnpm test:contract` 24/24 + `dotnet build … -c Release`; fresh setup persists `p2p` realtime+rawtx primary; wizard shows p2p default | Providers step remodeled (p2p primary default, external = fallback, JungleBus = block sync); DTO + store + generated types updated; no hand-mirrored type reintroduced | `audits/S5-slice-audit-prompt.md` |

## Definition of Done

- S1–S5 `done` (or explicit `not_opened` with rationale).
- A fresh wizard run (turnkey local mode) persists `p2p` as realtime +
  rawTx primary with no operator action; external providers fall back.
- With `p2p` primary and an empty peer pool, the journal still receives
  observations from the external runners (cold-start honest).
- rawTx for a mempool tx resolves via `p2p`; rawTx for a confirmed tx
  the peers won't serve auto-falls-back to an external provider.
- Admin metrics show per-source first-seen latency.
- `pnpm verify` + `pnpm test:contract` (24/24) + `dotnet build
  Dxs.Consigliere.sln -c Release` + `secrets-lint` all green.
- `audits/A1.md` + `evidence/closeout.md` written.

## Delivery Notes

Per-slice commit hashes recorded here at closeout (separate backfill
commit, never `--amend`):

| slice | commit | summary |
|---|---|---|
| S1 | `1c2ccda` | register p2p as routable provider + defaults |
| S2 | `8d65a1b` | P2P rawTx fetch + external auto-fallback |
| S3 | `e29f572` | all realtime sources concurrent; cold-start honest |
| S4 | none | not_opened — already delivered by Wave 4 source-metrics |
| S5 | `783726a` | wizard Providers remodel → p2p primary default |
| Audit folds | none | per-slice audit prompts written; no findings folded as of closeout |
