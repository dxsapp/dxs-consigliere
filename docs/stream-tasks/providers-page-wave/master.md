# Providers Page Wave

## Goal

Add a dedicated admin `Providers` page that makes `Consigliere` onboarding materially easier for new operators.

The page must:
- show the full supported provider landscape clearly
- explain recommended defaults
- allow minimal realtime and REST provider configuration
- explicitly promote the ecosystem providers that complete the missing infrastructure slice around `Consigliere`

This is not a generic config editor.

## Product Position

`Consigliere` is an open-source self-hosted scoped BSV indexer.
It closes a missing ecosystem slice, so the product must clearly show:
- which providers exist
- what each provider is good for
- which choices are recommended by default
- how to connect them without forcing a user to reverse-engineer config files

## Canonical Defaults

- Realtime default: `Bitails`
- REST default: `WhatsOnChain`

Optional realtime providers:
- `Bitails`
  - `websocket`
  - `zmq`
- `JungleBus`
- `ZMQ`
  - self-hosted node ZMQ or Bitails ZMQ as a service

Optional REST providers:
- `WhatsOnChain`
- `Bitails`

## Scope

In scope:
- dedicated `/providers` admin page
- recommended default block
- current effective provider setup block
- provider catalog cards for `Bitails`, `WhatsOnChain`, `JungleBus`, and `ZMQ`
- minimal configuration controls for realtime provider, REST provider, and provider-specific connection settings
- explicit provider help links and onboarding guidance
- persisted operator provider config outside static files
- effective provider-config read model for the page

Out of scope:
- generic config editor
- arbitrary capability-matrix editing
- free-form fallback-chain editing for every capability
- billing or purchase flows
- provider-secret vault management
- auto-discovery of provider plans or subscriptions

## Page Shape

The page should contain:

1. Recommended Setup
- Realtime: `Bitails`
- REST: `WhatsOnChain`
- short explanation why those are the safe defaults

2. Current Effective Setup
- active realtime provider
- active realtime transport
- active REST provider
- static vs override vs effective values
- whether restart is required

3. Provider Catalog
- one card each for:
  - `Bitails`
  - `WhatsOnChain`
  - `JungleBus`
  - `ZMQ`
- each card should show:
  - what the provider supports
  - whether it is recommended/default/advanced
  - current status
  - what is required to connect it
  - direct help links

4. Configuration Surface
- realtime provider select
- Bitails transport select when relevant
- REST provider select
- minimal provider-specific key/endpoint fields
- apply and reset actions

## Provider Positioning Rules

### `Bitails`
- recommended default for realtime ingest
- supports websocket and ZMQ modes
- can also be used for some REST-oriented paths
- page should explicitly show where to get started with Bitails and how Bitails ZMQ can be used as a service

### `WhatsOnChain`
- recommended default for REST / historical fetch
- straightforward onboarding path
- page should explain that it is the default REST provider in `Consigliere`

### `JungleBus`
- advanced realtime option
- should be positioned as useful but not the default onboarding path
- page should explain that setup may require provider-side preparation or subscription configuration

### `ZMQ`
- advanced infrastructure option
- should explain both self-hosted node ZMQ and Bitails ZMQ-as-a-service posture

## Ownership Model

Primary zones:
- `indexer-state-and-storage`
- `service-bootstrap-and-ops`
- `public-api-and-realtime`
- `frontend-admin-shell`

Supporting zones:
- `verification-and-conformance`
- `repo-governance`

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `PP1` | `indexer-state-and-storage` | `operator/state` | `done` | - | store tests | provider override document and storage/query seam exist |
| `PP2` | `service-bootstrap-and-ops` | `operator/runtime` | `done` | `PP1` | routing tests | effective provider config feeds realtime and bounded REST selection paths |
| `PP3` | `public-api-and-realtime` | `operator/api` | `done` | `PP1`,`PP2` | controller tests | `/api/admin/providers` read/apply/reset contract exists |
| `PP4` | `frontend-admin-shell` | `operator/ui` | `done` | `PP3` | frontend build + page QA | dedicated Providers page exists with catalog, defaults, config, and help links |
| `PP5` | `verification-and-conformance` | `operator/verification` | `done` | `PP2`,`PP3`,`PP4` | focused proof | static vs override vs reset vs required-fields behavior is covered |
| `PP6` | `repo-governance` | `operator/governance` | `done` | `PP5` | docs review | admin docs, onboarding docs, and wave ledger are updated honestly |

## Hard Boundaries

- do not turn this wave into a full source-routing editor
- do not expose arbitrary provider matrices or hidden expert toggles in v1
- do not mutate static files from the admin UI
- do not bury provider onboarding guidance inside raw technical diagnostics
- keep Runtime page focused on diagnostics; Providers page becomes the onboarding/configuration surface

## Definition of Done

- admin has a dedicated `Providers` page
- the page clearly explains all supported providers and their roles
- default recommendations are explicit:
  - `Bitails` for realtime
  - `WhatsOnChain` for REST
- operator can configure minimal provider settings from UI
- ecosystem help links are present and clear
- runtime diagnostics and provider onboarding are separated cleanly
- no generic config editor was introduced

## Validation Summary

Validated during closeout:
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminProvidersControllerTests|FullyQualifiedName~AdminRuntimeControllerTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests|FullyQualifiedName~RealtimeSourcePolicyOverrideStoreIntegrationTests|FullyQualifiedName~BitailsRealtimeIngestRunnerTests"`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm typecheck`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm build`

## Honest Residuals

- provider configuration now covers realtime primary provider, REST primary provider, Bitails transport, and bounded provider connection fields only
- `GET /api/admin/runtime/sources` remains as a compatibility/runtime-summary surface; `/providers` is the canonical onboarding and configuration page
- historical address and token scan execution still remains Bitails-backed in runtime and is not exposed as a UI-switchable policy in this wave
- persisted provider overrides require service restart before runtime selection changes are guaranteed to apply fully
