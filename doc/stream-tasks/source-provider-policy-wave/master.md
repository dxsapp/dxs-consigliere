# Source Provider Policy Wave

## Goal

Lock the upstream source product policy so future provider work does not drift away from the intended `Consigliere` posture.

This wave is docs-only.

It does not implement:
- new adapters
- new realtime transports
- capability enum expansion
- runtime routing changes

It defines the canonical product decision and the follow-up implementation envelope.

## Canonical Decisions

- `bitails` is the baseline provider-first realtime source
- `junglebus` is an optional advanced source, not the default managed-mode source
- `whatsonchain` is REST-only assist and fallback, not a realtime source
- future Bitails config should support `websocket` and `zmq` as transport modes inside one provider contract

## Touched Docs

- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-capability-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-config-examples.md`

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `SP1` | `repo-governance` | `operator/governance` | `done` | - | docs review | product source-policy is stated explicitly |
| `SP2` | `repo-governance` | `operator/governance` | `done` | `SP1` | docs review | capability matrix reflects Bitails baseline and WoC REST-only posture |
| `SP3` | `repo-governance` | `operator/governance` | `done` | `SP1` | docs review | config examples reflect the new default routing posture and future Bitails transport model |
| `SP4` | `service-bootstrap-and-ops` | `operator/platform` | `done` | `SP1`,`SP2`,`SP3` | focused config binding and startup-diagnostics tests | runtime config classes and validation support Bitails transport selection explicitly |
| `SP5` | `external-chain-adapters` | `operator/integration` | `done` | `SP4` | adapter-focused tests and routing proof | Bitails realtime transport seam is explicit and routing can select Bitails for realtime ingest when transport is configured |

## Definition of Done For This Wave

- the product policy is explicit and stable in docs
- examples no longer imply `junglebus`-first as the default provider posture
- future implementation work has a bounded contract to follow

## Closeout

- docs policy landed in commit `35f80f5`
- adapter seam landed in commit `4c2fcd6`
- config contract and startup diagnostics follow-up landed in the current wave closeout

Residual:
- this wave does not wire Bitails realtime transport into the live ingest loop
- realtime runtime integration remains a future `indexer-ingest-orchestration` task
