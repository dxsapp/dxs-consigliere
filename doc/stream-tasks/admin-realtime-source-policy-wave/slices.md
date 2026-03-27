# Slices

## `RS1` Persisted Override Layer

Create a narrow persisted override seam for realtime policy only.

Expected shape:
- `OperatorRealtimeSourceOverride` or equivalent
- persisted outside static config files
- stores only:
  - `PrimaryRealtimeSource`
  - `BitailsTransport`
  - `UpdatedAt`
  - `UpdatedBy`
  - optional `Version`

Rules:
- no generic config blob
- no secrets in the override doc
- no non-realtime capability overrides

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tracking/` or a dedicated adjacent operator-config seam

## `RS2` Effective Routing Consumption

Make runtime routing consume the override layer for `realtime_ingest` only.

Expected behavior:
- when no override exists, routing uses static config
- when override exists, `realtime_ingest` uses overridden primary source and/or Bitails transport
- other capabilities remain static-config-driven

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/SourceCapabilityRouting.cs`
- optional small helper service for effective realtime policy composition

## `RS3` Admin Read/Apply/Reset Contract

Expose a narrow admin API for the admin shell.

Read:
- `GET /api/admin/runtime/sources`

Write:
- `PUT /api/admin/runtime/sources/realtime-policy`
- `DELETE /api/admin/runtime/sources/realtime-policy`

Read DTO should include:
- static values
- override values
- effective values
- allowed options
- provider diagnostics
- `overrideActive`
- `restartRequired`

Write DTO should include only:
- `primaryRealtimeSource`
- `bitailsTransport`

Rules:
- reject invalid source or transport with `400`
- reject values outside the bounded allow-list

## `RS4` Focused Proof

Minimum proof set:
- no override -> effective policy equals static config
- override primary source only -> realtime routing changes, other capability routing does not
- override Bitails transport only -> effective transport changes
- reset -> override removed, static config restored
- admin endpoint returns static/override/effective split clearly
- invalid values are rejected with `400`

Likely test paths:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Controllers/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/`

## `RS5` Docs And Handoff

Update admin docs so Claude or another frontend agent can wire the page without guessing.

Must update:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-stack-and-rules.md`

Document explicitly:
- read-only fields vs mutable fields
- apply/reset semantics
- static vs override vs effective values
- restart/reload messaging
