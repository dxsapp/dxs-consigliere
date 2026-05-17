# Wave 1 Audit A2 Follow-up

Verdict: **MAJOR REVISION REQUIRED**

Audited revision: `4997122` plus documentation update `e7ec9cc` on
`codex/consigliere-vnext`.

## A2 Closure Table

| A2 finding | Status | Where the revision addresses it | Residual concern |
| --- | --- | --- | --- |
| H1 — W1 does not currently produce an authoritative mainnet header tip by default | **Partially closed** | `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs:40-48`, `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs:143-150`, and `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs:195-220` add `ExtendResult.Unanchored` and explicit `Seed(header,height)`. `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs:64-102` fetches a seed, persists it, and calls `chain.Seed(...)`. `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs:265-270` drops unanchored headers instead of persisting arbitrary height 0. `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs:31-39` binds `IHeadersBootstrapSource` to `WhatsOnChainHeadersBootstrapSource`. | The core false-height-0 failure is fixed, but the new default WoC source does not appear able to parse the live WoC header endpoint it calls; see new-H1. If bootstrap returns null, the service now fails closed rather than corrupting the store, but it still will not produce the authoritative W1 tip needed by W2. There are also stale comments in `HeadersChainBootstrapper.cs:21-32` and `HeadersChainBootstrapper.cs:117-130` still describing `NoopHeadersBootstrapSource` as the W1 default and pure-P2P cold start as the happy path. |
| H2 — S7 soak scaffold does not validate the production W1 path | **Partially closed** | `tests/Spikes/P2p/HeadersSoakRecorder/HeadersSoakRecorder.csproj:11-18` now references `Dxs.Consigliere`. `tests/Spikes/P2p/HeadersSoakRecorder/Program.cs:3-7` and `Program.cs:108-146` drive real `PeerManager`, `HeadersChainService`, `HeadersChainBootstrapper`, `HeadersChain`, and `IBlockHeaderStore` with an in-memory store and JSONL `INewBlockNotifier`. `Program.cs:208-217` queries `store.GetTipAsync()` at the end. `tests/Spikes/P2p/HeadersSoakRecorder/analyze.py:12-18` and `analyze.py:102-111` implement `INCONCLUSIVE` exit `2` for `<128` joined blocks. | The executable path is now the right path, but the README remains partly stale: `tests/Spikes/P2p/HeadersSoakRecorder/README.md:28-41` still documents `tip_hash` and WoC joins as wire-order, while `Program.cs:13-20` and `Program.cs:173-175` emit display-order hashes. `README.md:86-91` still says `analyze.fsx`; the committed analyzer is `analyze.py`. The harness also depends on the default WoC bootstrap source, so new-H1 can leave it unanchored. |
| H3 — External hash order is inconsistent with the WhatsOnChain/admin validation contract | **Closed** | `src/Dxs.Bsv/P2p/Chain/BlockHeaderHasher.cs:17-30` adds `ToDisplayHex`. `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs:314-324` builds `BlockTipDto` with display-order hash and prev hash. `src/Dxs.Consigliere/Controllers/AdminP2pController.cs:80-123` documents and converts Raven wire-order fields to display-order API responses. `tests/Dxs.Bsv.Tests/P2p/Chain/BlockHeaderHasherTests.cs:90-99` asserts the known genesis display hash. | No blocking residual. The controller tests still use symmetric byte patterns in places, but the conversion helper has direct known-hash coverage and the API boundary now applies it. |
| M1 — Several new tests pass locally without exercising the behavior they claim to protect | **Closed** | `tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj:19` adds `Xunit.SkippableFact`. The W1 Raven-gated tests now use `[SkippableFact]` plus `Skip.IfNot(...)`, for example `tests/Dxs.Consigliere.Tests/P2p/BlockHeaderStoreTests.cs:41-121`, `tests/Dxs.Consigliere.Tests/P2p/HeadersChainBootstrapperTests.cs:85-165`, `tests/Dxs.Consigliere.Tests/P2p/HeadersChainServiceTests.cs:129-216`, and `tests/Dxs.Consigliere.Tests/Controllers/AdminP2pControllerHeadersTests.cs:67-127`. `tests/Dxs.Consigliere.Tests/P2p/HeadersChainBootstrapperUnitTests.cs:17-181` adds non-Raven coverage over the bootstrapper and `IBlockHeaderStore` abstraction. | No blocking residual. The local runner now reports the W1 Raven path as explicit skips rather than silent passes. |

## New Findings

1. **H1 — The new default WhatsOnChain bootstrap source is incompatible with the live response shape**

   **Where:** `src/Dxs.Consigliere/Services/P2p/WhatsOnChainHeadersBootstrapSource.cs:13-18`, `src/Dxs.Consigliere/Services/P2p/WhatsOnChainHeadersBootstrapSource.cs:76-95`, `src/Dxs.Consigliere/Services/P2p/WhatsOnChainHeadersBootstrapSource.cs:104-121`; default DI binding at `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs:31-39`; bootstrap fallback behavior at `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs:80-83` and `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs:265-270`.

   **Evidence:** The implementation assumes `GET /v1/bsv/main/block/{hash}/header` returns either a 160-character raw header hex string or JSON containing a `hex` or `raw` property. I checked the live endpoint during this audit:

   ```
   curl -fsS -i https://api.whatsonchain.com/v1/bsv/main/block/<bestblockhash>/header
   ```

   It returned `HTTP/2 200` with a JSON block object containing fields such as `hash`, `height`, `version`, `merkleroot`, `time`, `nonce`, `bits`, and `previousblockhash`, but no raw 80-byte header hex and no `hex` or `raw` field. With that response, `ExtractHeaderHex(...)` returns `null`, `FetchAsync(...)` returns `null`, `HeadersChainBootstrapper.BootstrapAsync(...)` returns false, and `HeadersChainService` remains unanchored. Subsequent P2P headers are then deliberately dropped as `ExtendResult.Unanchored`.

   **Why it matters:** This reintroduces the operational part of A2 H1. The revised code no longer corrupts state by assigning height 0, but the default Wave 1 system still does not produce the authoritative current mainnet header tip that W2 needs for confirmation checks and S6/S7 need for validation.

   **Recommendation:** Make the default bootstrap source consume a response shape that is known to provide an 80-byte header. Options: use an endpoint that returns raw header hex; parse the live WoC block JSON fields into an 80-byte header with explicit endianness tests; or replace this with a Bitails/RPC-backed adapter that already exposes raw headers. Add unit coverage using the actual live-shaped WoC JSON object and an integration smoke that proves `WhatsOnChainHeadersBootstrapSource.FetchAsync(...)` returns a non-null `BootstrapSeed` with a hash matching `bestblockhash`.

## Verification

- `dotnet build Dxs.Consigliere.sln -c Release` — passed, 0 errors. A first parallel build attempt failed with an `obj` file lock while the soak build was running; rerunning sequentially passed.
- `dotnet build tests/Spikes/P2p/HeadersSoakRecorder/HeadersSoakRecorder.csproj -c Release` — passed, 0 errors.
- `dotnet test tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release --no-restore` — passed, 154/154.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --no-restore` — failed only the 3 known `TransactionStoreIntegrationTests` embedded-Raven runtime failures; 267 passed, 18 skipped, 3 failed.
- Static contract grep `rg -n "OnBlockInvReceived|OnInvReceived\(tx\)" src tests` — no matches.

## Closing Rationale

The revision closes the contract hygiene, test-reporting, hash-order, and most of the soak-harness concerns. It also fixes the dangerous height-0 behavior by making an unbootstrapped chain fail closed.

S0/S1 primitives are now safer, but Wave 1 still does not meet the required W2 handoff because the default cold-start bootstrap source appears nonfunctional against the live endpoint it calls. W2 (`bsv-mempool-observer-wave`) should **not** open until new-H1 is fixed and the S7 README is aligned with the implemented display-order/analyzer contract.
