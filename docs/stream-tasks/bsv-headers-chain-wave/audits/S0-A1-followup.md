# S0 Audit A1 Follow-up — Program-Wide Contract Freeze

Verdict: **APPROVE**

Reviewed fix commit: `78b05fa` — docs/evidence reconciliation for S0-A1 H1 and M1.

## Closure Table

| Finding | Status | Where addressed | Residual |
| --- | --- | --- | --- |
| H1 — `SendGetHeadersAsync` return-type mismatch | **closed** | `docs/stream-tasks/bsv-headers-chain-wave/slices.md:42-59` now makes `ValueTask SendGetHeadersAsync(GetHeadersMessage msg, CancellationToken ct)` the canonical S0.2 signature and records that the earlier `Task` spelling was a draft mistake. `src/Dxs.Bsv/P2p/Session/PeerSession.cs:212-221` implements the helper as `ValueTask`. `tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/manifest.json:55-68` freezes the return type as `System.Threading.Tasks.ValueTask`. `docs/stream-tasks/bsv-headers-chain-wave/evidence/S0.md:51-54` records the reconciliation. | None. The spec, implementation, and manifest now agree. |
| M1 — grep validation over-scoped to docs | **closed** | `docs/stream-tasks/bsv-headers-chain-wave/slices.md:297-314` scopes the forbidden historical callback grep to code only and records the exact command: `rg -n "OnBlockInvReceived|OnInvReceived\(tx\)" src tests`. `docs/stream-tasks/bsv-headers-chain-wave/launch-prompt.md:147-152` applies the same code-only validation rule. `docs/stream-tasks/bsv-headers-chain-wave/evidence/S0.md:9-21` records the command and zero-match exit code. | None. The validation is now reproducible without failing on intentional audit/documentation references. |

## New Findings

None.

Non-blocking evidence note: `docs/stream-tasks/bsv-headers-chain-wave/evidence/S0.md:6-7` still shows `<hash-pending>` for the docs-fix commit. That should be replaced with `78b05fa` in the next evidence touch, but it does not affect the frozen contract or the S0 gate.

## Verification

- `rg -n "OnBlockInvReceived|OnInvReceived\(tx\)" src tests` returned no matches.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --filter FullyQualifiedName~ContractFreezeApprovalTests --no-restore` — passed, 5/5.
- `dotnet test tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release --filter FullyQualifiedName~PeerSessionAdditiveDispatchTests --no-restore` — passed, 3/3.

## Closing Rationale

Both S0-A1 findings are closed. The contract-freeze source of truth now explicitly freezes `SendGetHeadersAsync` as `ValueTask`, matching `PeerSession` and the approval manifest, and the stale-callback grep is scoped to code only. No new gate-blocking defects were introduced by the fix commit.

S1-S7 may open.
