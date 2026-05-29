# thin-node S1 — slice-audit prompt

Audit target: S1 (register `p2p` as a routable provider + defaults).
Diff: `a9892af..1c2ccda` on `codex/consigliere-vnext`.

Read `master.md` (Product Decisions, Core Rule 1) + `slices.md` §S1 first.

## What landed
- `ExternalChainProviderName.P2p = "p2p"`.
- `P2pProviderDiagnostics : IExternalChainProviderDiagnostics` — descriptor
  `("p2p", [RealtimeIngest, RawTxFetch])`, health from `BsvP2pHealth`
  ready-peer count. Registered in `BsvP2pSetup`.
- `ConsigliereSourcesConfig`: `P2pSourceConfig` + `Providers.P2p`
  (Enabled=true, [realtime_ingest, raw_tx_fetch]); `Routing.PrimarySource`
  + `Capabilities.RealtimeIngest.Source` + `Capabilities.RawTxFetch.Source`
  → `p2p`. `CloneConfig` copies `Providers.P2p`.
- `SourceCapabilityRouting.CanServe` + `AdminProviderConfigService`
  `CanServeRealtime`/`CanServeRawTx`: gate `p2p` on `Providers.P2p.Enabled`.
- `AdminProviderConfigService`: recommendations → p2p; candidate arrays
  prepend p2p; new "Thin node (P2P)" catalog card.
- `ConsigliereConfigValidation`: p2p in `KnownProviders` + provider-states.

## Focus
1. **Gate coherence.** `p2p` routability is gated on
   `Providers.P2p.Enabled` (default true), NOT on the actual P2P subsystem
   master switch `BsvP2pConfig.Enabled` (default false). Confirm this
   decoupling is safe: when the subsystem is OFF but routing lists p2p
   primary, the rawTx fetch (S2) returns null → external fallback, and the
   realtime observer simply isn't running while external runners carry
   (S3). Is the decoupling acceptable, or should routability follow
   `BsvP2pConfig.Enabled`? (The audit may flag this as a Low.)
2. **No silent contract loosening.** Confirm RawTxFetch fallback default
   `[whatsonchain, junglebus, bitails]` keeps whatsonchain as the REST seed
   (`GetDefaultRestPrimaryProvider` reads FallbackSources[0]); and that the
   override-collapse in `ApplyOverride` (rawtx fallback → single REST
   primary) still yields a sane confirmed-tx fallback.
3. **Routing correctness.** `Resolve(RealtimeIngest|RawTxFetch)` returns
   p2p primary under defaults; falls to the next allowed when
   `Providers.P2p.Enabled=false`. (3 new tests in
   `SourceCapabilityRoutingTests`.)
4. **RequiresRestart.** p2p realtime primary → no restart required
   (correct? the P2P observer is its own hosted service, gated by
   appsettings not the wizard override).

## Validation evidence
- `dotnet build … -c Release` clean.
- `SourceCapabilityRoutingTests` 13/13 (+3 new); full backend suite —
  only pre-existing RavenTestDriver/production-DI env failures.

## Verdict + finding format
Verdict (`APPROVE`/`APPROVE WITH CHANGES`/`MAJOR REVISION`); findings
`C*|H*|M*|L*` with file:line, why, fix.
