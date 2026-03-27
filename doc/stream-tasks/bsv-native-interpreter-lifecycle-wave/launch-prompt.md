Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md) to execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/master.md`.

Constraints:
- Keep zone routing explicit.
- Treat this as a proof-expansion wave, not a new product-semantics wave.
- `L2` and `L3` stay primarily in `verification-and-conformance`; only minimal `bsv-protocol-core` changes are allowed, and only if an existing lifecycle flow proves they are required for native replay.
- Do not broaden interpreter behavior speculatively.
- Do not hide oracle-only branches behind broad green test output.
- `A1` must state exactly whether oracle demotion can be reopened after this wave, or what specific blind spots still remain.

Definition of done:
- broader DSTAS lifecycle suites have explicit native replay proof
- rooted and multisig-heavy DSTAS suites have explicit native replay proof
- any protocol-core support additions remain narrowly bounded to repo-needed lifecycle replay
- audit makes the post-wave oracle decision explicit
