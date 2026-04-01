# Bitails Realtime Ingest Wave

## Goal

Consume the Bitails realtime transport seam inside the live ingest loop so `Consigliere` can use Bitails as the default provider-first realtime source for managed-scope ingest.

This wave starts from the already landed policy and adapter groundwork:
- policy and config contract are already explicit
- Bitails realtime topic planning already exists
- the live ingest loop still remains JungleBus- and node-oriented

## Scope

In scope:
- realtime source selection for live ingest
- Bitails transport planning consumption inside runtime orchestration
- managed-scope subscription target derivation for Bitails realtime topics
- Bitails-driven mempool or near-realtime ingest into the existing `ITxMessageBus` / filter pipeline
- startup and hosted-task wiring needed for the new ingest path
- focused proof that runtime can ingest through Bitails without inventing a parallel pipeline

Out of scope:
- changing public API contracts
- rewriting token or address projection logic
- replacing block backfill strategy
- adding fake realtime support to WhatsOnChain
- broad JungleBus removal
- node/ZMQ redesign

## Ownership Model

Primary zone:
- `indexer-ingest-orchestration`

Supporting zones:
- `external-chain-adapters`
- `service-bootstrap-and-ops`
- `verification-and-conformance`

The wave must stay bounded:
- no broad provider refactor
- no mixed changes to unrelated admin/compose work already sitting in the worktree

## Dependency Context

Completed prerequisite wave:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/source-provider-policy-wave/master.md`

Key landed seams:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Configs/ConsigliereSourcesConfig.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/SourceCapabilityRouting.cs`

Current residual:
- Bitails can be selected by routing for `realtime_ingest`
- but the live runtime does not yet consume that route

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `BR1` | `indexer-ingest-orchestration` | `operator/runtime` | `done` | - | code review + focused runtime tests | runtime has one explicit Bitails realtime ingest entrypoint instead of hard-coded JungleBus-only flow |
| `BR2` | `external-chain-adapters` | `operator/integration` | `done` | `BR1` | adapter-focused tests | Bitails adapter exposes the minimum runtime-consumable observable/client shape needed by orchestration |
| `BR3` | `service-bootstrap-and-ops` | `operator/platform` | `done` | `BR1`,`BR2` | startup/build/config review | hosted-task and DI wiring select Bitails realtime path coherently from source routing and transport config |
| `BR4` | `verification-and-conformance` | `operator/verification` | `done` | `BR1`,`BR2`,`BR3` | focused proof | runtime ingest tests prove Bitails realtime path reaches the existing tx/filter pipeline |
| `A1` | `repo-governance` | `operator/governance` | `done` | `BR4` | audit note | wave closeout states whether Bitails is now the practical default live ingest source and what residual JungleBus role remains |

## Bounded Design Rules

- do not build a second ingest pipeline beside `ITxMessageBus`
- do not let Bitails runtime wiring bypass the existing transaction filter path
- do not hard-code provider choice where `SourceCapabilityRouting` should decide
- do not broaden the change into historical backfill or public API work
- keep JungleBus available unless the wave explicitly proves it can be demoted

## Definition of Done

- runtime can consume Bitails realtime transport planning in the live ingest loop
- provider selection for realtime ingest is policy-driven rather than JungleBus-hardcoded
- the ingest path still feeds the existing tx/filter pipeline
- focused validation proves Bitails realtime ingress works for the bounded runtime surface
- closeout states the remaining role of JungleBus after integration

## Outcome

- `RealtimeIngestBackgroundTask` is now the single runtime-owned realtime entrypoint.
- routing for `realtime_ingest` now selects between Bitails, JungleBus, or node/ZMQ without inventing a second pipeline.
- Bitails realtime scope derives managed-scope topics from tracked addresses and tracked tokens, then feeds the existing `ITxMessageBus` / `TransactionFilter` path.
- startup bootstrap no longer blindly starts node mempool and block feeds when policy says Bitails or JungleBus owns those capabilities.

## Validation Evidence

- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~BitailsRealtimeTopicCatalogTests|FullyQualifiedName~BitailsRealtimeTransportPlannerTests|FullyQualifiedName~BitailsProviderDiagnosticsTests|FullyQualifiedName~BitailsRealtimePayloadParserTests"`
  - passed `10/10`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~SourceCapabilityRoutingTests|FullyQualifiedName~RealtimeBootstrapPlannerTests|FullyQualifiedName~BitailsRealtimeIngestRunnerTests|FullyQualifiedName~BitailsRealtimeSubscriptionScopeProviderTests|FullyQualifiedName~ConsigliereConfigBindingTests"`
  - passed `20/20`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`
  - succeeded after serial rerun; dependency warning on `System.Text.Json 8.0.4` was later removed by a direct package override in `Dxs.Infrastructure`
