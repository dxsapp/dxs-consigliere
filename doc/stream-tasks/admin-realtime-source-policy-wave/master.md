# Admin Realtime Source Policy Wave

## Goal

Add a minimal operator-facing realtime source policy surface to the admin shell so an operator can:
- see the effective realtime routing decision
- override only the realtime primary source
- override only the Bitails transport
- reset overrides back to static config

This wave must not turn the admin shell into a generic config editor.

## Scope

In scope:
- read-only admin runtime sources summary for realtime policy
- persisted operator override for realtime policy only
- admin endpoints to read/apply/reset that override
- routing consumption of the override layer for `realtime_ingest`
- admin UI contract updates for the new panel/actions

Out of scope:
- editing secrets or provider URLs
- generic `appsettings.json` editing
- changing block backfill, validation, or raw-tx policy
- changing enabled capability matrices for providers
- historical/backfill source mutation
- arbitrary JSON config mutation

## Product Decisions

Operator-managed values in v1:
- `primaryRealtimeSource`
  - `bitails`
  - `junglebus`
  - `node`
- `bitailsTransport`
  - `websocket`
  - `zmq`

Operator cannot change in this wave:
- provider credentials
- provider URLs
- enabled/disabled provider capabilities
- fallback chains for non-realtime capabilities
- block backfill source
- validation or raw-tx provider routing

Override precedence:
- `effective routing = static config + operator realtime override`
- override applies only to the realtime policy surface

## Ownership Model

Primary zones:
- `indexer-state-and-storage`
- `indexer-ingest-orchestration`
- `public-api-and-realtime`

Supporting zones:
- `verification-and-conformance`
- `repo-governance`

Keep the wave bounded:
- no broad control-plane refactor
- no mutation of static files from the admin UI
- no secrets handling in the frontend

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `RS1` | `indexer-state-and-storage` | `operator/state` | `todo` | - | store tests | override document and storage/query contract exist |
| `RS2` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `RS1` | routing tests | effective realtime routing reads the override layer |
| `RS3` | `public-api-and-realtime` | `operator/api` | `todo` | `RS1`,`RS2` | controller tests | admin endpoints expose read/apply/reset contract |
| `RS4` | `verification-and-conformance` | `operator/verification` | `todo` | `RS1`,`RS2`,`RS3` | focused proof | static vs override vs reset behavior is proved |
| `RS5` | `repo-governance` | `operator/governance` | `todo` | `RS4` | docs review | admin UI/API docs reflect the new runtime sources panel and override semantics |

## UX Shape

Runtime Sources page should show:
- active realtime source
- Bitails transport
- fallback chain used for realtime routing
- provider diagnostics and health
- static values vs override values vs effective values
- whether an override is active
- whether restart or runtime refresh is required

Mutations in v1:
- `Set primary realtime source`
- `Set Bitails transport`
- `Reset realtime override`

UX rules:
- always toast for success/error results
- confirmation dialog before apply/reset
- current vs desired values visible before apply
- no raw JSON editor

## Definition of Done

- operator can see the effective realtime policy in admin UI
- operator can change only realtime primary source and Bitails transport
- overrides are persisted outside static files
- reset is supported
- `SourceCapabilityRouting` consumes the override layer for `realtime_ingest`
- docs and admin UI handoff contract are updated
