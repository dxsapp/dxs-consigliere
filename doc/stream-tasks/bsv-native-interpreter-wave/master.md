# BSV Native Interpreter Master Ledger

## Header

- Parent task: add a native BSV-profiled interpreter to reproduce current STAS/DSTAS script-valid truth locally
- Branch: `codex/consigliere-vnext`
- Current status: `planned`
- Slice plan: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/slices.md`
- Launch prompt: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/launch-prompt.md`
- Architectural context:
  - `/Users/imighty/Code/dxs-consigliere/doc/protocol/bsv-native-interpreter-target.md`
  - `/Users/imighty/Code/dxs-consigliere/doc/protocol/dstas-source-of-truth.md`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/master.md`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-95-wave/master.md`

## Goal

Build a native BSV-profiled execution subsystem in `Dxs.Bsv` that can locally validate the current repository STAS/DSTAS script-truth surface without relying on opaque out-of-repo execution.

Target outcome:
- native interpreter reproduces current deterministic oracle truth for repo-needed STAS/DSTAS paths
- `Dxs.Bsv` exposes a stable reusable evaluation API
- deepest protocol truth can be audited locally rather than only through vendored oracle artifacts

## Fixed Decisions

- `BSV` only, no multi-chain policy matrix in v1
- v1 scope is bounded to all script paths required by current STAS/DSTAS repo truth
- interpreter owns execution truth only, not token business semantics
- prevout access is through an explicit resolver abstraction
- deterministic oracle remains during parity phase as a regression anchor

## Non-Goals

- no claim of full generic BSV contract coverage in v1
- no migration of STAS/DSTAS semantic classification into the interpreter
- no coupling interpreter internals to Raven, provider APIs, or runtime services
- no silent removal of oracle fixtures before parity proves native equivalence
- no opportunistic runtime adoption in hot paths unless a later wave explicitly opens that integration

## Active Slices

| slice | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|
| `I01-task-package` | `operator/governance` | `todo` | - | docs review | durable interpreter package is opened and routed correctly |
| `I02-execution-contracts` | `operator/protocol` | `todo` | `I01-task-package` | build + contract tests | evaluation contracts exist for policy, results, and prevout resolution |
| `I03-vm-core` | `operator/protocol` | `todo` | `I02-execution-contracts` | unit tests | stack machine core exists with deterministic failure model |
| `I04-opcode-surface` | `operator/protocol` | `todo` | `I03-vm-core` | opcode tests | repo-needed push, control, stack, and comparison/script opcodes execute correctly |
| `I05-signature-engine` | `operator/protocol` | `todo` | `I03-vm-core`,`I04-opcode-surface` | crypto/signature tests | sighash, `CHECKSIG`, and `CHECKMULTISIG` work for repo-needed BSV profile |
| `I06-input-evaluation` | `operator/protocol` | `todo` | `I05-signature-engine` | input-pair tests | unlocking + locking evaluation works against resolved prevouts |
| `I07-transaction-api` | `operator/protocol` | `todo` | `I06-input-evaluation` | tx-level tests | stable transaction-level evaluation API returns deterministic results |
| `I08-oracle-parity` | `operator/verification` | `todo` | `I07-transaction-api` | parity suites | native interpreter matches current deterministic oracle on vendored truth fixtures |
| `I09-negative-paths` | `operator/verification` | `todo` | `I08-oracle-parity` | negative suites | invalid STAS/DSTAS flows fail locally with deterministic reasons |
| `I10-lifecycle-adoption` | `operator/verification` | `todo` | `I08-oracle-parity`,`I09-negative-paths` | lifecycle packs | conformance, protocol-owner, multisig, and lifecycle packs pass through native interpreter |
| `I11-public-api-and-docs` | `operator/protocol` | `todo` | `I10-lifecycle-adoption` | API/docs review | public evaluation API and usage docs are stable and discoverable |
| `A1-interpreter-audit` | `operator/governance` | `todo` | `I11-public-api-and-docs` | audit note | audit states whether native interpreter is sufficient to demote oracle from primary truth anchor |

## Evidence Paths

- Audits: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/audits/`
- Benchmarks: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/benchmarks/`
- Remediation: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/remediation/`
- Closeout: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/evidence/`

## Hard Stop Criteria

Stop downstream slices and open remediation before continuing when any of the following occurs:
- execution truth starts leaking STAS/DSTAS business semantics into the interpreter core
- sighash or `CHECKMULTISIG` behavior is still ambiguous after a slice claims support
- native interpreter results diverge from current deterministic oracle without an explicit adjudication path
- prevout resolution starts depending on Raven or runtime services
- v1 scope expands into unsupported generic BSV claims without parity evidence

## Key Risks

- crypto/signature verification is harder than the stack machine itself
- `CHECKMULTISIG` and sighash correctness can hide subtle drift for a long time
- parity packs can create false confidence if they skip known negative or edge cases
- there is a temptation to blur execution truth and protocol business meaning in DSTAS-heavy paths

## Evidence Log

| date | slice | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-27 | kickoff | task-package | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/master.md` | durable native interpreter package opened |
