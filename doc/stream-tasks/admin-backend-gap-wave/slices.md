# Slices

## `AB1` State And Store Seams

### Scope
- tracked addresses list query
- tracked tokens list query
- tracked address details read model
- tracked token details read model
- untrack/tombstone operations in registration store
- findings feed source shape from tracked status + rooted security state + failure reason
- dashboard summary source shape from tracked status documents and existing ops readers where cheap

### Notes
- prefer querying `TrackedAddressDocument` / `TrackedAddressStatusDocument`
- prefer querying `TrackedTokenDocument` / `TrackedTokenStatusDocument`
- do not purge history or projections on untrack in v1
- use tombstone semantics and keep operator-visible status honest

## `AB2` Admin API Contracts

### Scope
- add admin list endpoints
- add admin detail endpoints
- add admin remove/untrack endpoints
- add admin findings endpoint
- add admin dashboard summary endpoint
- keep auth policy on all new endpoints

### Transport rules
- return exact readiness/history/rooted status fields instead of new lossy abstractions
- accept simple query params for paging/filtering only if needed
- avoid introducing a second dashboard-specific domain model if a thin summary DTO is enough

## `AB3` Verification

### Must cover
- address list includes tracked state and history mode
- token list includes rooted security hints
- details endpoints surface readiness/history/rooted fields
- remove/untrack marks entity tombstoned and stops reporting it as active
- findings endpoint returns failure reasons and unknown-root findings
- dashboard summary counts tracked/degraded/backfilling/full-history entities correctly

## `AB4` Governance And Handoff

### Outputs
- update `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- update `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md` if any screen assumptions changed
- update this wave ledger with closeout notes
