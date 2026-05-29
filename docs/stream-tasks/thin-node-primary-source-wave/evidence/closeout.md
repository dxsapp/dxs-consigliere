# Thin-node-primary-source — closeout

## Result

The in-house BSV thin node is now a first-class routable provider `p2p`,
the default primary for realtime + rawTx ingest, with external providers
(Bitails/WhatsOnChain/JungleBus) demoted to automatic fallback and JungleBus
scoped to historical block backfill. All realtime sources run concurrently
(redundant + measurable, not failover), so a fresh host with a cold peer
pool never stalls — external runners carry while P2P warms. The first-run
wizard defaults to "Thin node (P2P)" with no operator-supplied URL/key.

## Key files

- `src/Dxs.Infrastructure/Common/ExternalChainProviderName.cs` — `P2p`.
- `src/Dxs.Consigliere/Services/P2p/P2pProviderDiagnostics.cs` — catalog
  descriptor + health.
- `src/Dxs.Consigliere/Services/P2p/{I,}P2pRawTransactionClient.cs` —
  getdata-based rawTx fetch, null-on-miss.
- `src/Dxs.Consigliere/Configs/ConsigliereSourcesConfig.cs` —
  `P2pSourceConfig` + p2p-primary capability defaults.
- `src/Dxs.Consigliere/Services/Impl/SourceCapabilityRouting.cs` +
  `Data/Runtime/AdminProviderConfigService.cs` — p2p gate, recommendations,
  candidates, card.
- `src/Dxs.Consigliere/Services/Impl/RawTransactionFetchService.cs` — p2p
  case + auto-fallback.
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/RealtimeIngestBackgroundTask.cs`
  — concurrent enabled-runner orchestration.
- `src/admin-ui/src/screens/setup-wizard/steps/Step2Providers.tsx` (+ mock
  seed) — wizard remodel.

## Behavioral summary

- Fresh wizard completion persists `p2p` as realtime + rawTx primary, no
  manual config, no domain/key.
- rawTx for a mempool/recent tx resolves via P2P `getdata`; a confirmed/
  historical tx the peers won't serve auto-falls-back to whatsonchain/
  junglebus/bitails.
- Bitails + JungleBus realtime runners keep observing in parallel with the
  always-on P2P observer; `/api/admin/metrics/sources` shows per-source
  first-seen wins + lag (who's fastest), p2p included.
- Prod posture / broadcast path unchanged; journal write contract + dedup
  untouched.

## Residuals (honest)

- **Cold-start P2P warm-up**: p2p is the default but ingest does not depend
  on the pool warming — external runners are always on (S3) and rawTx
  misses fall back (S2). p2p will show as lagging in the first-seen metric
  until peers accept.
- **Subsystem master switch**: routing defaults to p2p, but the P2P
  subsystem only RUNS when `BsvP2pConfig.Enabled=true` (e.g. the turnkey
  Local profile). With it off, p2p routes degrade to immediate external
  fallback — safe, documented in A1.
- **Live mainnet end-to-end** (real getdata hit, real first-seen race) is
  operator-run, evidence-pending.

## Delivery

| slice | commit | status |
|---|---|---|
| S1 register p2p provider + defaults | `1c2ccda` | done |
| S2 P2P rawTx fetch + fallback | `8d65a1b` | done |
| S3 concurrent realtime + cold-start | `e29f572` | done |
| S4 first-seen metric | none | not_opened — already delivered by Wave 4 |
| S5 wizard remodel | `783726a` | done |
