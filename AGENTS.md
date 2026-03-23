# Repository Routing

This repository uses explicit responsibility zones.

Start here:
- `doc/AGENTS.md`
- `doc/repository-zones/zone-catalog.md`
- `doc/repository-zones/ownership-matrix.md`

Rules:
- Determine the zone by changed paths before planning implementation.
- If a task touches multiple zones, split it into child tasks by zone and sequence them by dependency.
- Do not mix protocol parsing, persisted state, API contracts, and runtime orchestration in one change unless the task explicitly requires cross-zone integration.
- Use `doc/repository-zones/handoff-contract.md` when one zone blocks another.

Hotspots:
- `src/Dxs.Bsv/Script/Read/LockingScriptReader.cs`
- `src/Dxs.Bsv/BitcoinMonitor/Impl/TransactionFilter.cs`
- `src/Dxs.Consigliere/Services/Impl/TransactionStore.cs`
- `src/Dxs.Consigliere/Startup.cs`

CODEOWNERS:
- Active GitHub CODEOWNERS is not enabled yet because team handles are not defined.
- Use `.github/CODEOWNERS.template` as the source of truth for path mapping until handles are assigned.
