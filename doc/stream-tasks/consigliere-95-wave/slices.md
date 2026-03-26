# Consigliere 9.5+ Slices

## Wave Order

1. `W1-raven-semantic-collapse`
2. `W2-projection-consumers-only`
3. `W3-protocol-policy-decomposition`
4. `W4-rooted-history-feature-seam`
5. `W5-self-hosted-protocol-truth`
6. `W6-history-sync-productization`
7. `W7-storage-maturity`
8. `A1-score-audit`

## Execution Slices

### `W1-raven-semantic-collapse`

- zone: `indexer-state-and-storage`
- goal: remove DSTAS/STAS business classification from Raven patch scripts
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/TransactionStorePatchScripts.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/TransactionStore.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Transactions/MetaTransaction.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/**`
- completion:
  - patch script only persists pre-derived fields
  - one canonical derived transaction state exists in C#
- expected score lift:
  - semantic single-source-of-truth `+2.0`
  - DSTAS AI-first total `+0.4`

### `W2-projection-consumers-only`

- zone: `indexer-state-and-storage`
- goal: make token/address projections consume canonical derived tx state rather than infer protocol semantics again
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/**`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`
- completion:
  - projectability, protocol type, and per-tx validation are taken from canonical derived state or narrow adapters over it
  - rooted filtering stays separate from protocol classification
- expected score lift:
  - runtime consumer clarity `+1.5`
  - future-agent edit safety `+0.8`

### `W3-protocol-policy-decomposition`

- zone: `bsv-protocol-core`
- goal: decompose `StasProtocolLineageEvaluator` into small policy components
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/**`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Validation/**`
- completion:
  - issue, dependency, redeem, event, and optional-data rules are explicit policy seams
  - evaluator is orchestration only
- expected score lift:
  - protocol discoverability `+0.7`
  - protocol edit safety `+1.0`

### `W4-rooted-history-feature-seam`

- zone: `verification-and-conformance`
- goal: extract rooted DSTAS verification into one obvious feature seam
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/`
  - mixed rooted suites that currently host DSTAS assertions
- completion:
  - trusted/unknown-root mixed flows, partial/full semantics, and rebuild parity are discoverable under `Dstas/RootedHistory`
- expected score lift:
  - verification discoverability `+0.8`
  - rooted token confidence `+0.8`

### `W5-self-hosted-protocol-truth`

- zone: `bsv-protocol-core`
- goal: make deepest protocol truth self-hosted or deterministically enforced in-repo
- owned paths:
  - protocol execution or oracle enforcement seams
  - corresponding conformance packs and fixtures
- completion:
  - script-valid truth does not depend on opaque out-of-repo assumptions
- expected score lift:
  - platform protocol self-sufficiency `+2.0`
  - overall platform confidence `+0.4`

### `W6-history-sync-productization`

- zone: `indexer-state-and-storage`
- goal: finish the history sync subsystem so readiness, authority, and backfill semantics are fully productized
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tracking/**`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/**`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/**`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/**`
- completion:
  - history sync lifecycle is complete for selective scope
  - readiness and coverage are authoritative and operationally observable
- expected score lift:
  - history maturity `+1.0`
  - runtime operability `+0.5`

### `W7-storage-maturity`

- zone: `platform-common` + `service-bootstrap-and-ops`
- goal: finish storage economics and operational strategy beyond comfortable Raven-first defaults
- owned paths:
  - storage config/docs/ops surfaces
  - benchmark and evidence packs
- completion:
  - retention, archival, rebuild, and storage pressure policies are explicit and measured
- expected score lift:
  - storage maturity `+1.0`
  - platform confidence `+0.3`

### `A1-score-audit`

- zone: `repo-governance`
- goal: record final scorecard, achieved lifts, and residual blockers if any dimension remains `< 9.5`
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-95-wave/audits/`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-95-wave/evidence/`
- completion:
  - final audit says which scores crossed `9.5`
  - residuals are explicit rather than implied

## Validation By Wave

- `W1`: state/query contract tests, Raven parity tests, DSTAS persistence suites
- `W2`: projection tests, rooted token readers, address projection integration tests
- `W3`: protocol validation packs, conformance vectors, protocol-owner fixtures
- `W4`: rooted-history focused tests, mixed trusted/unknown-root DSTAS lifecycle tests
- `W5`: deep protocol truth packs, script evaluation packs, reproducible oracle drift tests
- `W6`: history sync integration tests, readiness API tests, backfill lifecycle tests
- `W7`: storage benchmarks, rebuild/backfill evidence, ops surface tests
- `A1`: score audit plus closeout summary
