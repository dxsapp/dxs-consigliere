# BSV Native Interpreter Slices

## Slice Order

1. `I01-task-package`
2. `I02-execution-contracts`
3. `I03-vm-core`
4. `I04-opcode-surface`
5. `I05-signature-engine`
6. `I06-input-evaluation`
7. `I07-transaction-api`
8. `I08-oracle-parity`
9. `I09-negative-paths`
10. `I10-lifecycle-adoption`
11. `I11-public-api-and-docs`
12. `A1-interpreter-audit`

## Execution Slices

### `I01-task-package`

- zone: `repo-governance`
- goal: open and route the interpreter wave as a durable package
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/protocol/bsv-native-interpreter-target.md`
- completion:
  - task package exists
  - fixed decisions, risks, hard stops, and evidence paths are explicit

### `I02-execution-contracts`

- zone: `bsv-protocol-core`
- goal: create clear execution contracts before any VM implementation
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
- completion:
  - `IPrevoutResolver` exists
  - `BsvScriptExecutionPolicy` exists
  - `ScriptEvaluationResult` and `TransactionEvaluationResult` exist
  - contracts compile without pulling in runtime services

### `I03-vm-core`

- zone: `bsv-protocol-core`
- goal: add the stack machine and execution context
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
- completion:
  - stack, alt-stack, and conditional execution state are implemented
  - deterministic error codes exist for VM-level failures
  - VM core is testable independent of STAS/DSTAS fixtures

### `I04-opcode-surface`

- zone: `bsv-protocol-core`
- goal: implement the opcode surface needed by current repo flows
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
  - supporting script opcode helpers in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/**`
- completion:
  - push/control/stack/basic comparison and other repo-needed opcodes are implemented
  - unsupported out-of-scope opcodes fail explicitly rather than silently

### `I05-signature-engine`

- zone: `bsv-protocol-core`
- goal: add BSV-profiled sighash and signature verification
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
  - extracted helpers from `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Transactions/Build/**` as needed
- completion:
  - deterministic preimage generation exists
  - `CHECKSIG` works against resolved prevouts
  - `CHECKMULTISIG` works for repo-needed flows, including owner/multisig cases

### `I06-input-evaluation`

- zone: `bsv-protocol-core`
- goal: evaluate unlocking + locking script pairs against prevouts
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
- completion:
  - input evaluation executes unlocking script, then locking script, under the BSV policy profile
  - results include deterministic failure reasons and minimal debug data

### `I07-transaction-api`

- zone: `bsv-protocol-core`
- goal: expose a stable transaction-level evaluation service
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
  - public-facing exports in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/**`
- completion:
  - `TransactionEvaluationService` or equivalent public API exists
  - transaction-level evaluation does not depend on storage/network code

### `I08-oracle-parity`

- zone: `verification-and-conformance`
- goal: prove parity against the existing deterministic oracle
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Shared/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/**`
- completion:
  - current vendored oracle fixtures can be replayed through the native interpreter
  - parity assertions are stable and explicit

### `I09-negative-paths`

- zone: `verification-and-conformance`
- goal: add native failure proofs for invalid flows
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/**`
- completion:
  - invalid STAS/DSTAS flows fail locally
  - failure reasons are deterministic enough to audit regressions

### `I10-lifecycle-adoption`

- zone: `verification-and-conformance`
- goal: run the existing protocol truth packs through the native interpreter
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Shared/**`
- completion:
  - conformance vectors pass
  - protocol-owner and multisig packs pass
  - master lifecycle critical flows pass under the interpreter path

### `I11-public-api-and-docs`

- zone: `bsv-protocol-core`
- goal: make the interpreter a discoverable first-class subsystem
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/protocol/**`
- completion:
  - public API is stable and documented
  - an agent can find the native interpreter entrypoint and required validation packs quickly

### `A1-interpreter-audit`

- zone: `repo-governance`
- goal: record whether the native interpreter is sufficient to reduce oracle dependence
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/audits/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/evidence/**`
- completion:
  - audit states achieved capability, residual gaps, and whether oracle can be demoted from primary truth anchor

## Validation By Slice

- `I02`: contract tests + compile-only checks
- `I03`: VM stack/context unit tests
- `I04`: opcode behavior tests for repo-needed surface
- `I05`: sighash/signature and multisig tests
- `I06`: input pair evaluation tests with explicit prevout resolvers
- `I07`: transaction-level API tests
- `I08`: deterministic oracle parity tests
- `I09`: negative-path and failure-code suites
- `I10`: conformance, protocol-owner, multisig, and lifecycle packs via native interpreter
- `I11`: API/docs review + discoverability checks
- `A1`: final audit and closeout evidence
