Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md) to execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/master.md`.

Constraints:
- Keep zone routing explicit.
- `P1` stays primarily in `verification-and-conformance`; only minimal `bsv-protocol-core` helper changes are allowed if they are necessary for deterministic resolver composition.
- Do not use network, Raven, or runtime services to complete missing prevouts.
- Do not skip incomplete protocol-owner tx as the end state.
- `P2` must document only behavior actually proven by tests.
- `P3` must make a hard oracle-role decision or state precisely why demotion is still blocked.

Definition of done:
- protocol-owner native parity no longer relies on skipping incomplete prevout cases
- native interpreter public surface is documented and stable
- audit explicitly states oracle role after this wave
