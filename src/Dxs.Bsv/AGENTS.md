# Dxs.Bsv Routing

This project is split into two zones:

- `bsv-runtime-ingest`
  - Paths: `BitcoinMonitor/**`, `Rpc/**`, `Zmq/**`, `Factories/**`
  - Focus: node connectivity, buses, filtering, throughput, runtime behavior

- `bsv-protocol-core`
  - Paths: everything else in `Dxs.Bsv`
  - Focus: transaction models, parsing, scripts, STAS/DSTAS semantics, builders

Rules:
- Do not put STAS/DSTAS protocol semantics into runtime ingest classes.
- Do not put RPC, ZMQ, or bus orchestration logic into script/parser classes.
- Cross-zone changes inside `Dxs.Bsv` need explicit justification and a validation plan.

Hot files:
- `Script/Read/LockingScriptReader.cs`
- `Script/Read/UnlockingScriptReader.cs`
- `BitcoinMonitor/Impl/TransactionFilter.cs`
- `Tokens/Stas/StasProtocolTransactionFactory.cs`
