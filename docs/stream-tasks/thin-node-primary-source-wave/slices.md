# Slices — thin-node primary source

Source of truth for ledger + decisions: `master.md`. This file is the
execution decomposition. Co-author trailer on every commit:
`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

On ambiguity, make the smallest safe judgment call that preserves
behaviour, document it in `audits/A1.md`, and continue. Do NOT halt to
ask unless a truly blocking contradiction emerges.

## Overview

Verified anchors (read these before editing — confirm current shape):
- `src/Dxs.Consigliere/Services/Impl/SourceCapabilityRouting.cs` —
  `CanServe` (L155) special-cases `node` (L162) via
  `sourcesConfig.Providers.Node.Enabled`; everything else resolves
  through the catalog descriptor (L168) + per-provider config switch
  (L174-180, returns null → false for unknown providers).
- `src/Dxs.Consigliere/Services/Impl/RawTransactionFetchService.cs` —
  `TryFetchFromProviderAsync` (L71) switch; `node` case (L77) is the
  template for a `p2p` case. The `TryGetAsync` loop (L45-59) already
  treats null as "try next" and only throws after a real error.
- `src/Dxs.Consigliere/Data/Runtime/AdminProviderConfigService.cs` —
  recommendation constants (L19-21), candidate arrays (L23-41).
- `src/Dxs.Infrastructure/Common/ExternalChainProviderCatalog.cs` —
  catalog = every registered `IExternalChainProviderDiagnostics.Descriptor`.
- `src/Dxs.Infrastructure/{Bitails,JungleBus,WoC}/*ProviderDiagnostics.cs`
  — the descriptor pattern to copy.
- `src/Dxs.Consigliere/Services/P2p/` — `BsvP2pHealth`,
  `P2pMempoolIngestRunner` (inv→getdata→tx→watchlist→journal),
  `TxRelayCoordinator` (broadcast getdata/tx serving),
  `PerSessionDispatcherRegistry` (fan-out of `PeerSession` frames).
- `src/Dxs.Consigliere/Setup/{ExternalChainAdaptersSetup,BsvP2pSetup}.cs`,
  `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs`.
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/RealtimeIngestBackgroundTask.cs`
  + `*RealtimeIngestRunner.cs` — confirm whether it runs ONLY the
  resolved primary (the thing S3 changes).
- `src/Dxs.Consigliere/Data/Models/Metrics/SourceMetricsSnapshot.cs`,
  `src/Dxs.Consigliere/Controllers/AdminMetricsController.cs`.
- `src/admin-ui/src/screens/setup-wizard/steps/Step2Providers.tsx`,
  `setup-wizard.store.ts`; `src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs`.

---

## S1 — Register `p2p` as a routable provider + defaults

**Intent.** Make the thin node selectable/routable as a provider named
`p2p` with capabilities `RealtimeIngest` + `RawTxFetch`, default-primary
for both, gated on `BsvP2pConfig.Enabled`.

**Owned paths.** `SourceCapabilityRouting.cs` (CanServe gate only),
`ExternalChainProviderName.cs`, new
`src/Dxs.Infrastructure/P2p/P2pProviderDiagnostics.cs` (or nearest
existing P2p infra namespace), `ExternalChainAdaptersSetup.cs` (catalog
DI registration), `AdminProviderConfigService.cs` (recommendation
constants + candidate arrays).

**Exact task.**
1. Add `public const string P2p = "p2p";` to `ExternalChainProviderName`.
2. New `P2pProviderDiagnostics : IExternalChainProviderDiagnostics`:
   `Descriptor = new("p2p", [ExternalChainCapability.RealtimeIngest,
   ExternalChainCapability.RawTxFetch])`; `GetHealthAsync` bridges
   `BsvP2pHealth` (ready-peer count → healthy/degraded). Register it in
   the catalog DI alongside the other diagnostics.
3. `SourceCapabilityRouting.CanServe`: add a `p2p` branch mirroring the
   `node` branch — routable only when `BsvP2pConfig.Enabled` (thread the
   flag in the same way `Providers.Node.Enabled` is read; if it's not on
   `ConsigliereSourcesConfig`, read it from the P2P config the cleanest
   available way and document the choice in A1). The descriptor's
   capability check then does the rest.
4. `AdminProviderConfigService`: `RecommendedRealtimeProvider` and
   `RecommendedRawTxProvider` → `ExternalChainProviderName.P2p`. Add
   `p2p` as the FIRST entry of `CandidateRealtimePrimarySources` and
   `CandidateRawTxPrimaryProviders`. Leave `RecommendedRestProvider`
   (whatsonchain) and the REST candidates unchanged.

**What not to do.** Do NOT touch the realtime runner orchestration
(that's S3). Do NOT hide `p2p` from the wizard here (S5 owns the UI).
Do NOT remove or alter the `node` provider.

**Validation.** `dotnet build Dxs.Consigliere.sln -c Release`. Unit:
`SourceCapabilityRouting.Resolve(RealtimeIngest|RawTxFetch)` returns
`p2p` primary when enabled; returns the next candidate when
`BsvP2pConfig.Enabled = false`.

**Completion signal.** Routing resolves `p2p` primary for both
capabilities under default config; catalog `GetDescriptors()` includes
`p2p`.

---

## S2 — P2P rawTx fetch + external auto-fallback

**Intent.** Fetch raw tx bytes from peers via `getdata`, returning null
on miss so the existing fallback loop reaches external providers.

**Owned paths.** New `IP2pRawTransactionClient` + impl under
`src/Dxs.Consigliere/Services/P2p/`, `RawTransactionFetchService.cs`
(add the `p2p` case + constructor dep).

**Exact task.**
1. `IP2pRawTransactionClient.TryGetRawAsync(txId, ct) : Task<byte[]>`.
   Impl: pick ready sessions from `BsvP2pHealth.ActiveSessions`, send
   `getdata(MSG_TX, txid)`, subscribe a one-shot `tx`-frame handler via
   `PerSessionDispatcherRegistry` (the same primitive
   `P2pMempoolIngestRunner` / `TxRelayCoordinator` use), await the first
   matching `tx` frame up to a bounded timeout (config, default e.g.
   3 s). Return bytes on hit; **null** on timeout / `notfound` / no ready
   peers. Verify the returned tx's hash matches the requested txid before
   returning.
2. `RawTransactionFetchService.TryFetchFromProviderAsync`: add
   `ExternalChainProviderName.P2p => await p2pRawTxClient.TryGetRawAsync(...)`.
   Inject the client.

**What not to do.** Do NOT throw on a P2P miss (Core Rule 2) — that
would abort the fallback loop. Do NOT block broadcast serving in
`TxRelayCoordinator`. Do NOT add P2P historical/block fetch.

**Validation.** Unit: a fake P2P client returning bytes → result.Provider
== "p2p"; returning null → service tries the next provider and returns
its bytes; the loop's existing throw-after-error path stays intact. Build
Release.

**Completion signal.** `p2p` primary rawtx fetch hits for a served txid;
misses transparently fall back to junglebus/whatsonchain.

---

## S3 — All realtime sources concurrent; cold-start honest

**Intent.** Making `p2p` the realtime primary must not silence the
external runners; all configured realtime sources observe in parallel
(Decision 2), so a cold P2P pool never stalls ingest (Core Rule 4).

**Owned paths.** `RealtimeIngestBackgroundTask.cs` + the per-provider
realtime runners; P2P observer wiring only to confirm it's always-on.

**Exact task.**
1. Confirm current behaviour: does `RealtimeIngestBackgroundTask` run
   ONLY the resolved primary runner? If so, change it to run every
   *enabled* external realtime runner (Bitails + JungleBus) concurrently,
   regardless of which is the routed primary. The P2P observer already
   runs via its own hosted service (`BsvP2pSetup`) — confirm and leave.
2. The journal is already source-aware (`SeenBySources`) and dedupes by
   txid, so concurrent appends are safe — add a test proving multi-source
   accumulation for the same txid rather than changing the write path.
3. "Primary" remains the attribution/quorum anchor; document precisely
   what still keys off the primary (if anything) so S4's metric and any
   state-transition logic stay correct.

**What not to do.** Do NOT change the journal write contract or dedup.
Do NOT add a new realtime source. Do NOT make external runners
conditional on P2P health (that's the rejected "failover" option —
operator chose always-on).

**Validation.** Integration/unit: with `p2p` primary + both external
realtime providers enabled, all run; with `BsvP2pHealth` reporting 0
ready peers, the external runners still append to the journal. Build
Release.

**Completion signal.** No realtime runner is gated on being the primary;
cold-start ingest proven peer-independent.

---

## S4 — Per-source first-seen latency metric

**Intent.** Record which source observed each txid first and how far the
others lagged; surface "who saw it first" in the admin metrics screen.

**Owned paths.** `SourceMetricsSnapshot.cs` + the first-seen attribution
writer (extend the existing source-metrics path from
`observation-source-metrics-wave`), `AdminMetricsController.cs`, the
admin-ui metrics screen + its store/types.

**Exact task.**
1. On each observation, compare against the txid's first-seen record:
   if first, stamp source + timestamp; else record this source's lag vs
   the first. Aggregate per-source: observation count, first-seen wins,
   median/p95 lag. Reuse the existing `SourceMetricsSnapshot` shape where
   possible; extend minimally.
2. Expose via `AdminMetricsController` (the existing metrics/sources
   endpoint) and render in the admin-ui metrics screen.

**What not to do.** Do NOT build a new persistence collection if the
existing source-metrics snapshot can carry it. Keep it observational —
no behavioural coupling to routing.

**Validation.** Contract green (regen if the metrics DTO changes). Unit:
given interleaved observations of one txid from p2p/bitails/junglebus,
the earliest is credited the first-seen win and others' lag is recorded.
`pnpm verify`.

**Completion signal.** Admin metrics screen shows per-source first-seen
wins + latency.

---

## S5 — Wizard Providers remodel → p2p primary default

**Intent.** The first-run Providers step reflects the new model: thin
node is the primary realtime+rawtx default, external providers are
fallback, JungleBus is the block-sync source.

**Owned paths.** `SetupCompleteRequest.cs` + setup DTOs,
`SetupWizardService.cs` (options/defaults assembly only — NOT the
recommendation constants, which S1 owns), `Step2Providers.tsx`,
`setup-wizard.store.ts`, generated types (`swagger.json` +
`api.generated.ts` regen).

**Exact task.**
1. Surface `p2p` in the allowed realtime + rawtx primary options
   (`SetupWizardService` stops filtering it out the way `node` is
   filtered — confirm the current filter and add `p2p` as a visible,
   default-selected option).
2. Reframe the Providers step UI: realtime + rawtx primary default to
   "Thin node (P2P)"; the external provider sub-forms are labelled as
   fallbacks; the JungleBus section is framed as historical block sync
   (the dedicated "Block sync" step 3 already collects the block
   subscription id — keep it).
3. Update `SetupProviderSelectionRequest` / store / `buildRequest` so a
   completed wizard persists `p2p` as realtime+rawtx primary. If a P2P
   field is needed (none expected — P2P needs no URL/key), don't invent
   one. Regenerate types; keep `types/{admin,auth}.ts` as generated
   re-exports (no hand-mirrored interface — wave-A4 S3 invariant).

**What not to do.** Do NOT edit `AdminProviderConfigService`
recommendation constants (S1). Do NOT reintroduce a hand-mirrored DTO.
Do NOT change the Block-sync step's contract.

**Validation.** `pnpm verify` (ends with `contracts:check`) +
`pnpm test:contract` 24/24 + `dotnet build … -c Release`. Manual/contract:
`GET /api/setup/options` lists `p2p` as the default realtime+rawtx
primary; `POST /api/setup/complete` with defaults persists `p2p`.
`secrets-lint` 0.

**Completion signal.** Fresh wizard → p2p primary persisted, external
shown as fallback, no domain/key needed for P2P.

---

## Dependency order

1. **S1** first (provider-routing foundation — everything references
   `p2p` existing as a routable provider).
2. **S2** and **S3** in parallel after S1 (disjoint zones:
   provider-routing rawtx vs realtime-orchestration).
3. **S4** after S3 (needs multi-source observations to be real).
4. **S5** after S1 (UI reflects the backend defaults S1 sets); can run
   parallel with S2/S3/S4 — disjoint zone — but validate last since it
   regenerates contracts.

Max 3 parallel subagents. Disjoint ownership: `provider-routing` (S1→S2),
`realtime-orchestration` (S3), `source-metrics` (S4), `setup-wizard`
(S5). S1 must close before S2/S5 start (both read `p2p` from routing/
defaults).

## Validation matrix

| slice | backend | frontend | proof |
|---|---|---|---|
| S1 | `dotnet build … -c Release` | — | routing unit: p2p primary on/off by `BsvP2pConfig.Enabled` |
| S2 | `dotnet build … -c Release` | — | fetch unit: p2p hit; p2p null → external fallback |
| S3 | `dotnet build … -c Release` | — | runner test: all concurrent; 0-peer cold-start still ingests |
| S4 | `dotnet build … -c Release` | `pnpm verify` | first-seen attribution unit + metrics surface |
| S5 | `dotnet build … -c Release` | `pnpm verify` + `test:contract` 24/24 | setup options/complete persists p2p primary |

End-to-end: fresh turnkey local stack → wizard (p2p default) → watched
address indexes; rawtx mempool hit via p2p, confirmed-tx fallback to
external; metrics screen shows first-seen wins.

## Closeout requirements

- All ledger rows `done` or `not_opened`.
- Commit hashes in `master.md` Delivery Notes (separate backfill commit).
- `audits/A1.md` (what landed, validated, residuals) +
  `evidence/closeout.md` (result, key files, behaviour, honest residuals
  — esp. the cold-start P2P warmup window).
- Per-slice `audits/S<n>-slice-audit-prompt.md` for the codex audit
  cadence.
