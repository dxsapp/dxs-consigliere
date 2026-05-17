# Wave 1 Closeout — `bsv-headers-chain-wave`

Status: ready for wave-level audit A2.

## Delivery summary

All 8 slices implemented and committed under
`docs/stream-tasks/bsv-headers-chain-wave/`. Build green; existing
test baselines preserved.

| slice | commit | files added/changed |
|---|---|---|
| S0 (contract freeze) | `c7b1428`, `78b05fa` (audit fix) | `src/Dxs.Bsv/P2p/Chain/{PeerTelemetry,IPeerTelemetrySink,NullPeerTelemetrySink}.cs`, `src/Dxs.Bsv/P2p/Session/PeerSession.cs` (callbacks + SendGetHeadersAsync + Telemetry property), `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs` (direct session.Telemetry calls), `src/Dxs.Consigliere/WebSockets/{IWalletHub,IWalletServer,WalletHub,BlockTipDto,ReorgEventDto,BroadcastReceiptDto}.cs`, `tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/{manifest.json,ContractFreezeApprovalTests.cs}`, `tests/Dxs.Bsv.Tests/P2p/Session/PeerSessionAdditiveDispatchTests.cs` |
| S1 (HeadersChain pure logic) | `aafbec6` | `src/Dxs.Bsv/P2p/Chain/{BlockHeaderHasher,HeadersChain,HeadersChainOptions}.cs`, `tests/Dxs.Bsv.Tests/P2p/Chain/{BlockHeaderHasherTests,HeadersChainTests,HeaderTestUtil}.cs` |
| S2 (BlockHeaderStore) | `79aaaf6` | `src/Dxs.Consigliere/Data/Models/P2p/BlockHeaderDocument.cs`, `src/Dxs.Consigliere/Data/P2p/BlockHeaderStore.cs`, `tests/Dxs.Consigliere.Tests/P2p/BlockHeaderStoreTests.cs` |
| S3 (HeadersChainService) | `332ca13` | `src/Dxs.Consigliere/Services/P2p/{HeadersChainService,INewBlockNotifier}.cs`, `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` (+ActiveSessions), `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (DI), `tests/Shared/MiniBsvServer.cs` (moved + made public), `tests/Dxs.Consigliere.Tests/P2p/HeadersChainServiceTests.cs` |
| S4 (Bitails bootstrap abstraction) | `407e1fb` | `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs` (+ `IHeadersBootstrapSource` + `NoopHeadersBootstrapSource`), `tests/Dxs.Consigliere.Tests/P2p/HeadersChainBootstrapperTests.cs` |
| S5 (HubNewBlockNotifier) | `7cbf2d7` | `src/Dxs.Consigliere/Services/P2p/HubNewBlockNotifier.cs`, `tests/Dxs.Consigliere.Tests/P2p/HubNewBlockNotifierTests.cs`, `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (DI swap from Null → Hub) |
| S6 (admin endpoints) | `8036bb7` | `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (+ /headers/tip, /headers/recent, `HeadersTipDto`), `tests/Dxs.Consigliere.Tests/Controllers/AdminP2pControllerHeadersTests.cs` |
| S7 (soak harness scaffold) | `8babc11` | `tests/Spikes/P2p/HeadersSoakRecorder/{HeadersSoakRecorder.csproj,Program.cs,README.md,analyze.py}` |

## Build + test results (commit `8babc11`)

- `dotnet build Dxs.Consigliere.sln -c Release` → 0 errors.
- `dotnet build tests/Spikes/P2p/HeadersSoakRecorder/HeadersSoakRecorder.csproj -c Release` → 0 errors.
- `dotnet test tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release`
  → 149/149 when run with the `--filter` per-class; 1 intermittent
  failure observed when running the full suite under load
  (`PeerManager_FailureRecordsNegativeCooldown`, network-timing
  test that loops on a 500 ms unroutable-IP connect timeout). Passes
  consistently in isolation. **Pre-existing flake**, not introduced
  by W1.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release`
  → 279/282. 3 failures all in `TransactionStoreIntegrationTests`
  on the local-runtime path (require external RavenDB embedded
  runtime). **Pre-existing baseline residual** — same 3 failures
  recorded against the S0 baseline and confirmed by audit
  `audits/S0-A1.md` §Verification.

## Test counts gained in W1

- `Dxs.Bsv.Tests`: +24 unit tests (S0 additive-dispatch +3, S1
  hasher + chain +21).
- `Dxs.Consigliere.Tests`: +19 tests (S0 contract-freeze +5; S2
  store +6, gated on .NET 8 runtime; S3 service +3, gated; S4
  bootstrapper +5, gated; S5 hub +1; S6 controller +4, gated).

Total `Dxs.Consigliere.Tests` count grew from 263 baseline → 282
(+19 visible tests; gated ones short-circuit on machines without
the embedded-Raven runtime per the existing repo convention).

## Static checks (slices.md §S0)

```
$ rg -n "OnBlockInvReceived|OnInvReceived\(tx\)" src tests
$ echo $?
1
```

Zero hits. No leftover historical-split surfaces in code.

```
$ rg -n "SendGetHeadersAsync" src/Dxs.Bsv/P2p/Session/PeerSession.cs
216:    public ValueTask SendGetHeadersAsync(GetHeadersMessage msg, CancellationToken ct) => SendAsync(P2pCommands.GetHeaders, msg.Serialize(), ct);
```

Required send-helper present.

## Contract-freeze surface (S0)

`tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/manifest.json`
snapshots:

- `IWalletHub` client callbacks: `OnNewBlock(BlockTipDto)`,
  `OnReorg(ReorgEventDto)`.
- `IWalletServer` server methods: `SubscribeToBlockTip`,
  `SubscribeToReorg`.
- `PeerSession` frozen members: `OnHeadersReceived`,
  `OnInvReceived`, `OnRejectReceived`, `SendGetHeadersAsync`,
  `Telemetry`.
- `PeerTelemetry` 15 fields (including `RejectByClass` map,
  getdata serve p50/p95, relay-back-inv count,
  protocol-violation count).
- `IPeerTelemetrySink` 10 rich-event methods.
- `BlockTipDto`, `ReorgEventDto`, `BroadcastReceiptDto`.

`ContractFreezeApprovalTests` (5 tests) enforce byte-equal match
against manifest plus three negative assertions on the
`IWalletHub` / `IWalletServer` / `WalletHub` split (audit A1
followup M1 guard).

## Handoff facts unlocked for W2-W6

The W2-W6 consumer table in `master.md` §Ownership Zones describes
what each wave consumes. Concrete artifacts now exist:

- **W2** can consume `PeerSession.OnInvReceived(InvMessage)` and
  `OnRejectReceived` to ingest mempool tx; `BlockHeaderStore` for
  tx-confirm checks.
- **W3** can consume `OnInvReceived(InvType.Block)` +
  `OnHeadersReceived` for reorg detection; can fill the
  `OnReorg(ReorgEventDto)` body (subscription group already wired);
  `BlockHeaderStore.RecentAsync`/`GetByHashAsync` for ancestor walks.
- **W4** can read `IPeerTelemetrySink.Snapshot()` per session for
  source metrics; aggregates already include the rich-event fields
  it will need.
- **W5** can collapse `IWalletServer.Broadcast` /
  `BroadcastTracked` into a single `Broadcast(hex) →
  BroadcastReceiptDto` — the DTO is frozen, server-method
  signatures are not (per S0.9 / handoff table).
- **W6** can swap the `IPeerTelemetrySink` construction site inside
  `PeerManager` (W6 owns that file) to a production sink, reading
  the per-session signals already wired by S0.

## What did NOT land (per scope)

- Reorg recovery — W3.
- Tx-confirm checks against the header chain — W2 ties tx to a
  `BlockHeaderStore` lookup; W1 only ships the store.
- Source metrics counter document + admin SPA page — W4.
- `Broadcast` server-method collapse — W5.
- Peer scoring loop, alerts, runbook, change notes — W6.
- Real Bitails REST `/chain/info` fetch — abstracted behind
  `IHeadersBootstrapSource`. W1 default is `NoopHeadersBootstrapSource`
  (pure-P2P cold start is the supported happy path); the Bitails
  adapter lands when operators run real warm-start tests.
- Admin UI page for headers — not in W1 scope (W4 ships the admin
  SPA page alongside source metrics).
- 24h soak result — recorder + analyzer are scaffolded; the actual
  24h run is operator-driven on a VPS and the report will land in
  `evidence/headers-soak.md` when complete.

## Residuals to track

- `evidence/headers-soak.md` pending the operator 24h run.
  Reproducibility rules and pass gate are locked in
  `slices.md` §S7 and in `tests/Spikes/P2p/HeadersSoakRecorder/README.md`.
- Bitails REST `/chain/info` adapter for `IHeadersBootstrapSource`
  is intentionally deferred (the W1 default `NoopHeadersBootstrapSource`
  is correct for cold-start P2P). Operators wanting warm-start
  wire a small REST shim — no W1 code change needed.
- `PeerManager_FailureRecordsNegativeCooldown` is intermittently
  flaky under load (unrelated to W1 changes; passes in isolation).
- `TransactionStoreIntegrationTests` (3 tests) require external
  RavenDB embedded runtime — pre-existing baseline.

## Audit trail

- `audits/wave1-audit-A1.md` — Codex pre-execution wave audit
  (MAJOR REVISION REQUIRED, 7 findings — all addressed).
- `audits/wave1-audit-A1-followup.md` — first follow-up (2 new
  findings — addressed).
- `audits/wave1-audit-A1-followup-2.md` — second follow-up (APPROVE).
- `audits/S0-A1.md` — S0 slice-level audit (MAJOR REVISION
  REQUIRED, 2 findings — addressed in `78b05fa`).
- `audits/S0-A1-followup.md` — S0 follow-up (APPROVE).
- `audits/wave1-audit-A2.md` — pending wave-level post-execution
  audit.

## Ready-for-audit checklist

- [x] All 8 slices `done`.
- [x] Build returns 0 errors on the full solution + the soak
      recorder.
- [x] No new failures vs the pre-W1 baseline (intermittent
      `PeerManager` test is pre-existing).
- [x] Manifest approval test green; additive-dispatch regression
      green; grep checks green.
- [x] `evidence/closeout.md` (this file) lists per-slice commit
      hashes, residuals, and handoff facts.
- [ ] `evidence/headers-soak.md` — operator-driven 24h soak; not
      in scope for code-review audit pass.
- [ ] `audits/wave1-audit-A2.md` — to be written by Codex against
      this evidence + the implementation commits.
