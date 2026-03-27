# Admin Backend Gap Wave

## Goal

Close the backend gaps that currently block a complete operator admin shell.

This wave is intentionally backend-only.
It fills the missing contracts that the admin UI needs, without mixing frontend implementation into the same execution stream.

## Product Frame

Consigliere is an open-source self-hosted scoped BSV indexer.
The admin shell is an operator tool.
These endpoints exist to remove Swagger/Raven dependence for normal daily operations.

## Confirmed Missing Contracts

1. tracked addresses list
2. tracked tokens list
3. tracked address details aggregate endpoint
4. tracked token details aggregate endpoint
5. remove/untrack address
6. remove/untrack token
7. findings / recent failures feed
8. dashboard aggregate summary

## Zones

- `indexer-state-and-storage`
- `public-api-and-realtime`
- `verification-and-conformance`
- `repo-governance`

## Dependency Order

1. state/read-model slice first
2. API transport slice second
3. verification slice third
4. governance docs closeout last

## Design Constraints

- no frontend work in this wave
- no fake aggregate contracts that duplicate existing public read models unnecessarily
- admin endpoints stay status-first and operator-oriented
- keep rooted token semantics explicit in dashboard/details surfaces
- remove/untrack must be honest about what it does: tombstone/stop tracking, not destructive history purge
- findings feed should start from already persisted failure/security state where possible, not from log scraping

## Proposed Endpoint Surface

### Lists
- `GET /api/admin/tracked/addresses`
- `GET /api/admin/tracked/tokens`

### Details
- `GET /api/admin/tracked/address/{address}`
- `GET /api/admin/tracked/token/{tokenId}`

### Mutations
- `DELETE /api/admin/tracked/address/{address}`
- `DELETE /api/admin/tracked/token/{tokenId}`

### Operator summaries
- `GET /api/admin/dashboard/summary`
- `GET /api/admin/findings`

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `AB1` | `indexer-state-and-storage` | `operator/state` | `todo` | - | query/store tests | read-model and store seams exist for tracked lists, details, and untrack tombstoning |
| `AB2` | `public-api-and-realtime` | `operator/api` | `todo` | `AB1` | controller tests | admin endpoints exist for lists, details, remove, findings, and dashboard summary |
| `AB3` | `verification-and-conformance` | `operator/verification` | `todo` | `AB1`,`AB2` | focused tests | tracked admin surface is covered end-to-end at controller/service level |
| `AB4` | `repo-governance` | `operator/governance` | `todo` | `AB2`,`AB3` | docs review | admin UI API map and wave ledger reflect the new contracts honestly |

## Definition of Done

- admin UI no longer lacks backend contracts for tracked lists/details/remove
- dashboard and findings have real backend endpoints
- remove/untrack semantics are explicit and tested
- Claude can build the shell without inventing missing backend behavior
