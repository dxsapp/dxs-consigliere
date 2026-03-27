# BSV Native Interpreter Follow-Up Slices

## Slice Order

1. `P1-protocol-owner-prevout-completion`
2. `P2-public-api-and-docs`
3. `P3-oracle-demotion-audit`

## `P1-protocol-owner-prevout-completion`

- zone: `verification-and-conformance`
- goal: remove the current partiality in native protocol-owner parity
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Shared/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/**`
  - only minimal production helper additions under `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**` if required for resolver composition
- completion:
  - explicit prevout completion strategy exists
  - protocol-owner tx with incomplete fixture prevouts are no longer skipped
  - native parity tests cover complete protocol-owner chains end to end
- acceptable implementation forms:
  - chain-local prevout derivation from fixture tx graph
  - deterministic composed resolver over fixture prevouts plus derived tx outputs
  - fixture augmentation with reproducible provenance
- disallowed forms:
  - network calls
  - Raven lookups
  - silent fallback to “skip this tx”

## `P2-public-api-and-docs`

- zone: `bsv-protocol-core`
- goal: stabilize the native interpreter as a discoverable first-class subsystem
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/protocol/bsv-native-interpreter-target.md`
  - optional supporting doc under `/Users/imighty/Code/dxs-consigliere/doc/protocol/`
- completion:
  - public entrypoints are clearly documented
  - policy surface is documented, including `AllowOpReturn` and supported forkid variants
  - trace contract is documented as optional debug metadata
  - docs state exactly what repo-needed scope is proven today

## `P3-oracle-demotion-audit`

- zone: `repo-governance`
- goal: make the oracle-role decision explicit and auditable
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/audits/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/evidence/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/master.md`
- completion:
  - audit states whether native interpreter is now primary local execution truth
  - oracle role is explicitly classified as either:
    - still primary truth anchor
    - or demoted to regression/backstop anchor
  - residual risks and missing proofs, if any, are listed plainly

## Validation

- `P1`:
  - focused `Dxs.Bsv.Tests` native parity pack
  - protocol-owner full-chain replay proof
- `P2`:
  - `Dxs.Bsv` build
  - relevant `Dxs.Bsv.Tests` pack still green
  - doc review against actual supported behavior
- `P3`:
  - audit note references concrete validation output and commits

