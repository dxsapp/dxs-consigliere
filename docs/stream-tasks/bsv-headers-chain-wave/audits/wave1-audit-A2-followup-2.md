# Wave 1 Audit A2 Follow-up 2

Verdict: **APPROVE**

Audited revision: `774aaa4` plus ledger update `f87da92` on
`codex/consigliere-vnext`.

## Closure Table

| Item | Status | Where the revision addresses it | Residual |
| --- | --- | --- | --- |
| new-H1 — `WhatsOnChainHeadersBootstrapSource` incompatible with live WoC `/block/{hash}/header` JSON shape | **Closed** | `src/Dxs.Consigliere/Services/P2p/WhatsOnChainHeadersBootstrapSource.cs:17-35` now documents the structured JSON contract. `src/Dxs.Consigliere/Services/P2p/WhatsOnChainHeadersBootstrapSource.cs:63-113` fetches `chain/info`, fetches `block/{bestblockhash}/header`, reconstructs header bytes, and self-verifies the computed display hash against `bestblockhash` before returning a `BootstrapSeed`. `src/Dxs.Consigliere/Services/P2p/WhatsOnChainHeadersBootstrapSource.cs:121-219` implements `TryBuildHeaderBytes`, including little-endian writes for version/time/bits/nonce and display-to-wire reversal for `previousblockhash` and `merkleroot`. `tests/Dxs.Consigliere.Tests/P2p/WhatsOnChainHeadersBootstrapSourceTests.cs:17-146` adds 10 cases covering BSV genesis JSON reconstruction, missing required fields, numeric bits, fake-handler end-to-end fetch, and hash-mismatch fail-closed behavior. | No blocking residual. I also re-checked the live WoC endpoints during audit: `chain/info` returned `blocks`, `headers`, and `bestblockhash`; `block/<bestblockhash>/header` returned structured JSON with `hash`, `height`, `version`, `merkleroot`, `time`, `nonce`, `bits`, and `previousblockhash`, which is the shape the parser now consumes. |
| Prior H1 residual — stale bootstrapper comments still described `NoopHeadersBootstrapSource` as production default | **Closed** | `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs:15-36` now says cold start must anchor to an authoritative source and names `WhatsOnChainHeadersBootstrapSource` as W1 production default. `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs:120-142` now scopes `NoopHeadersBootstrapSource` to tests / intentional cold-start hosts. | The option name remains `SeedFromBitails`, but this is a pre-existing naming artifact and not a W2 blocker because behavior and comments now identify the WoC default. |
| Prior H2 residual — S7 README still documented wire-order joins and `analyze.fsx` | **Closed** | `tests/Spikes/P2p/HeadersSoakRecorder/README.md:24-43` now documents `tip_hash` and `prev_hash` as display-order and says both `p2p` and `woc` records join byte-equal without conversion. `tests/Spikes/P2p/HeadersSoakRecorder/README.md:88-94` now names `analyze.py` and its exit codes. | No blocking residual. The operator-driven 24h soak evidence remains separate from this code-review approval, as already scoped in the wave closeout. |

## New Findings

None.

## Verification

- `dotnet build Dxs.Consigliere.sln -c Release` — passed, 0 errors.
- `dotnet build tests/Spikes/P2p/HeadersSoakRecorder/HeadersSoakRecorder.csproj -c Release` — passed, 0 errors.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WhatsOnChainHeadersBootstrapSourceTests` — passed, 10/10.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --no-restore` — failed only the known embedded-Raven runtime failures in `TransactionStoreIntegrationTests`; 277 passed, 18 skipped, 3 failed.
- Live WoC shape check on 2026-05-17: `chain/info` returned current `bestblockhash`; `block/<bestblockhash>/header` returned structured header JSON with the fields consumed by `TryBuildHeaderBytes`.

## Closing Rationale

The A2-followup blocker is closed: the production default bootstrap source now consumes the actual live WoC response shape and fails closed if reconstructed bytes do not hash back to `bestblockhash`. The H1 safety behavior from the previous revision remains intact, and the H2 reproducibility docs now match the recorder implementation.

Wave 2 (`bsv-mempool-observer-wave`) may open. No further Wave 1 contract-freeze amendment is required.
