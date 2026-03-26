# DSTAS AI-First Refactor Target

## Target outcome

Make DSTAS cheap for an AI agent to enter, modify, and validate without repo-wide semantic guessing.

## Target architecture

### `bsv-protocol-core`

Create an obvious DSTAS feature seam under:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Models/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Parsing/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Validation/`

The protocol core should export one canonical semantic contract, for example:
- locking semantics
- unlocking semantics
- derived event classification
- redeem policy result
- frozen state result
- optional-data continuity result

### `indexer-state-and-storage`

`Dxs.Consigliere` should stop acting like a second hidden semantic engine.
It should map and persist the canonical DSTAS contract through dedicated adapters.

Preferred shape:
- DSTAS mappers under `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/Dstas/`
- DSTAS runtime adapters under `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/Dstas/`

### `verification-and-conformance`

Tests should be discoverable by feature, not only by layer.
Preferred layout:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Parsing/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Validation/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/Persistence/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/Projection/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/`

## Non-goals

- no semantic protocol rewrite
- no rooted-history rewrite
- no opportunistic API redesign unless a slice explicitly requires it

## Completion bar

DSTAS is AI-first enough when:
1. an agent can find canonical DSTAS truth from one short doc and one obvious namespace seam
2. protocol semantics are not hidden inside generic STAS naming only
3. Raven patch parity is explicit and tested against the canonical contract
4. runtime readers no longer carry buried protocol heuristics
5. verification is discoverable as a feature map
