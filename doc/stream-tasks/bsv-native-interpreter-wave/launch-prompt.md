[$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md)

Work in `/Users/imighty/Code/dxs-consigliere` on branch `codex/consigliere-vnext`.

Goal:
add a native BSV-profiled interpreter to `Dxs.Bsv` so the repository can locally reproduce current STAS/DSTAS script-valid truth.

Primary specs:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/slices.md`
- `/Users/imighty/Code/dxs-consigliere/doc/protocol/bsv-native-interpreter-target.md`
- `/Users/imighty/Code/dxs-consigliere/doc/protocol/dstas-source-of-truth.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-95-wave/master.md`

Hard rules:
- build a BSV-profiled interpreter only; do not open a multi-chain policy matrix in v1
- keep v1 scope honest: repo-needed STAS/DSTAS script paths, not arbitrary generic BSV contracts
- keep interpreter responsibility limited to execution truth; do not move STAS/DSTAS business semantics into the VM
- do not couple prevout resolution to Raven, providers, or runtime services
- keep the current deterministic oracle during parity adoption
- do not claim success because a VM exists; success requires parity against current vendored truth fixtures

Execution order:
1. `I02-I07` first to establish contracts, VM core, opcode surface, signature engine, and public evaluation API
2. `I08-I10` next to prove parity, negative-path behavior, and lifecycle adoption
3. `I11` after the API surface is real and discoverable
4. `A1` only after focused validation evidence exists across protocol and verification packs

Required first output:
- one-table plan for `I01-I11` plus `A1`
- explicit zone routing and dependencies
- risks around sighash, `CHECKMULTISIG`, and oracle parity
- evidence paths and hard stops
- then immediate execution
