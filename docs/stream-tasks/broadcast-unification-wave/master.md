---
created: 2026-05-18
closed: 2026-05-18
type: wave
parent: consigliere-thin-node-observer-program
status: CLOSED — Codex audit chain A2 → A2-followup APPROVE WITH CHANGES (closed)
---

# Wave 5 — Broadcast Unification

## Goal

Collapse the legacy multi-provider HTTP-broadcast tier
(BitcoindService / Bitails / WhatsOnChain "try each in turn until
one accepts") into the single canonical P2P-driven path
(`TxRelayCoordinator.AnnounceAsync` from W2 S5). After this wave
closes, every transaction broadcast through Consigliere — REST,
SignalR, internal services — goes through `BroadcastService`'s
single `SubmitAsync` method which announces on the W2 P2P
infrastructure. The HTTP-broadcast client methods are deleted; the
Raven `Broadcast` document model used for multi-attempt
bookkeeping becomes vestigial.

Business outcome: Consigliere becomes a true thin-node observer +
broadcaster — no longer dependent on third-party REST APIs for tx
submission. Operator monitoring (W4 metrics) shows a single
broadcast surface to alert on; reliability + cost both improve
because Consigliere no longer pays Bitails / WhatsOnChain per
broadcast attempt.

## Product Decision

Per `vnext` repo policy (no backwards-compatibility), the legacy
`Broadcast(string)` / `Broadcast(Transaction)` overloads on
`IBroadcastService` are REMOVED outright (not deprecated /
shimmed). External REST + SignalR consumers using the legacy
routes will see compile-time / runtime errors and must migrate to
the unified path. The migration path is documented in this wave's
closeout for downstream wallets.

The frozen `BroadcastReceiptDto` (W1 S0.8 contract freeze) is the
single return DTO. The legacy `Broadcast` Raven document model is
left in place as a historical record but no new instances are
written (the multi-attempt bookkeeping was the only writer);
admin queries can still read old documents for forensic
purposes.

## Scope

In scope:

- **External entrypoint consolidation (S1)**.
  - `WalletHub.Broadcast(string)` (legacy, returns Raven
    `Broadcast` doc) — DELETED.
  - `WalletHub.BroadcastTracked(string)` (P2P path, returns
    `BroadcastReceiptDto`) — DELETED; replaced by a single
    unified `WalletHub.Broadcast(string rawHex)` returning
    `BroadcastReceiptDto`.
  - `TransactionController.POST /api/tx/broadcast/{raw}` —
    REMOVED. A new `POST /api/tx/broadcast` accepting a JSON body
    `{ "rawHex": "..." }` lands as its replacement, returning
    `BroadcastReceiptDto`.
- **`BroadcastService` collapse (S2)**.
  - DELETE: `Broadcast(string)`, `Broadcast(Transaction)`,
    `BroadcastToProviderAsync`, `BroadcastToNodeAsync`,
    `BroadcastToBitailsAsync`, `BroadcastToWhatsOnChainAsync`,
    `ResolveBroadcastTargetsAsync`.
  - KEEP: `SatoshisPerByte()` (called by fee-estimator),
    `SubmitAsync(rawHex, clientConnectionId, ct)`.
  - RENAME: `SubmitAsync` → `BroadcastAsync` so the canonical
    surface name matches the wave / hub method. The full new
    interface:
    ```csharp
    public interface IBroadcastService
    {
        Task<decimal> SatoshisPerByte();
        Task<BroadcastReceiptDto> BroadcastAsync(
            string rawHex,
            string? clientConnectionId = null,
            CancellationToken cancellationToken = default);
    }
    ```
  - The ctor drops `IBitailsRestApiClient`,
    `IWhatsOnChainRestApiClient`, and (post A2 L1)
    `IDocumentStore` dependencies. Survivors: `IBitcoindService`
    (fee-rate forwarder for `SatoshisPerByte`) + `ILogger`
    (2 deps total, down from 10). The P2P-side dependencies
    (`IBroadcastPolicyValidator`, `IOutgoingTransactionRepository`,
    `ITxAnnouncer` — interface-typed post A2 M1) are property-
    injected from W2 via the wirer.
- **HTTP broadcaster client removal (S3)**.
  - `IBroadcastProvider` interface in `Dxs.Bsv` — REMOVED. The
    interface had a single concrete impl (`BitcoindService`)
    which no longer broadcasts.
  - `BitcoindService.Broadcast(string)` — DELETED. Class retains
    `SatoshisPerByte()` (fee estimation only).
  - `IBitailsRestApiClient.Broadcast(...)` + impl + the response
    DTO — DELETED.
  - `IWhatsOnChainRestApiClient.BroadcastAsync(...)` + impl —
    DELETED.
  - DI registrations for these clients stay (they're still used
    for non-broadcast endpoints: fee, block, history); only the
    broadcast-specific method is excised.
- **Test cleanup (S4)**.
  - `BroadcastServiceTests` —
    - DELETE: `Broadcast_Succeeds_WhenAnyConfiguredProviderAccepts`,
      `Broadcast_Fails_WhenAllConfiguredProvidersReject`,
      `Broadcast_FailsClearly_WhenNoBroadcastProviderIsConfigured`.
    - ADD: `BroadcastAsync_PersistsToOutgoingStore_ReturnsReceipt`,
      `BroadcastAsync_FiresRelayCoordinator_AnnounceAsync`,
      `BroadcastAsync_PolicyValidationFails_ReturnsRejectedReceipt`,
      `BroadcastAsync_NoReadyPeers_ReturnsValidatedReceipt_NoAnnounceCount`.
- **Grep regression pin (S5)**.
  - New test
    `BroadcastUnificationGrepTests.NoLegacyBroadcastCallsRemain`
    that uses reflection / source-text inspection to assert:
    - No public method named `Broadcast(string)` or
      `Broadcast(Transaction)` on `IBroadcastService` /
      `BroadcastService`.
    - No `IBroadcastProvider` interface in `Dxs.Bsv`.
    - No `Broadcast` / `BroadcastAsync` method on the Bitails /
      WhatsOnChain client interfaces.
  - Pins the W5 done-when "grep shows no legacy broadcast
    HTTP-provider paths" at the test level.
- **DI + admin endpoint regression (S6)**.
  - Extend existing `BsvP2pSetupDiResolutionTests` or add
    `BroadcastZoneDiResolutionTests`: `IBroadcastService` resolves;
    the new ctor (no Bitcoind / Bitails / WoC client deps) works
    in production DI graph.
  - Controller test: `POST /api/tx/broadcast` returns
    `BroadcastReceiptDto` with `State=Validated` for a valid raw
    tx fixture.
- **Live mainnet validation (S7)**.
  - Operator-driven; defer per the W2 S8 / W3 S7 / W4 S8 pattern.
  - Pass condition: submit a real mainnet tx via the new unified
    endpoint; confirm it lands in a block within 2-3 block
    intervals.

Out of scope:

- **Legacy `Broadcast` Raven document migration.** The doc model
  stays in the DB schema for forensic queries on historical
  broadcasts; no new instances are written. A future ops wave can
  archive / delete old docs.
- **Fee estimation rework.** `SatoshisPerByte()` still polls the
  HTTP providers' fee endpoints; that's a separate concern from
  broadcast and is out of scope here.
- **`OutgoingTransactionMonitor` retry policy.** W3's
  rebroadcaster + W4's metrics already cover the
  observe-and-retry loop. W5 doesn't change retry semantics.
- **Wallet client migration code.** External wallets calling the
  legacy `Broadcast` REST / SignalR methods must migrate
  themselves; W5 ships a one-line `MIGRATION.md` snippet in the
  closeout for documentation only.
- **OpenTelemetry export of broadcast metrics.** W4 / W6 concern.

## Core Rules

1. **No new contract surface.** `BroadcastReceiptDto` (W1 S0.8
   freeze) is the single return shape. The new
   `IBroadcastService.BroadcastAsync` signature is a rename of
   the existing `SubmitAsync`; no field additions / removals on
   `BroadcastReceiptDto`.
2. **No HTTP fallback.** When no P2P peers are Ready, the new
   `BroadcastAsync` STILL succeeds at the policy-validation +
   persist-to-OutgoingStore layer; the announce-count is zero
   and the W3 rebroadcaster picks up the tx on the next reorg /
   peer-connection event. No silent HTTP fallback to bitcoind /
   Bitails / WoC.
3. **All broadcast paths through one method.** REST + SignalR +
   internal services all call `IBroadcastService.BroadcastAsync`.
   No duplicate validation / persistence / announce code paths.
4. **Vnext breaking-change policy.** Legacy method removals are
   compile-time errors for consumers. No `[Obsolete]` shimming.
   Closeout documents the migration.
5. **Frozen DTOs unchanged.** `BroadcastReceiptDto`,
   `BroadcastStateEvent` shapes per W1.
6. **W2 + W4 prereq dependency.** W2 closed (TxRelayCoordinator
   live). W4 closed (broadcast metrics surfaced via
   `OrphanedTxRebroadcastRecorder` / observation counters). W5
   touches W2's announce path read-only and adds no new W4
   counters.
7. **Stop-and-audit per wave** (program rule). S0 gets its own
   slice-level audit before S1+ open; S1-S6 covered by the
   wave-level audit `audits/wave5-audit-A1.md`.

## Ownership Zones

| Program zone | Repo zone | Files (edit unless noted) |
|---|---|---|
| `consigliere-broadcast` | `indexer-write-path` | `src/Dxs.Consigliere/Services/IBroadcastService.cs` (collapse); `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs` (collapse) |
| `consigliere-hub-public` | `public-api-and-realtime` | `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (delete legacy + rename) |
| `consigliere-rest-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/TransactionController.cs` (replace route) |
| `bsv-runtime-ingest` | `bsv-runtime-ingest` | `src/Dxs.Bsv/IBroadcastProvider.cs` (DELETE); `src/Dxs.Consigliere/Services/Impl/BitcoindService.cs` (drop Broadcast method) |
| `external-providers` | `Dxs.Infrastructure` | `src/Dxs.Infrastructure/Bitails/{IBitailsRestApiClient,BitailsRestApiClient}.cs` (drop Broadcast); `src/Dxs.Infrastructure/WoC/{IWhatsOnChainRestApiClient,WhatsOnChainRestApiClient}.cs` (drop BroadcastAsync) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Consigliere.Tests/Services/Impl/BroadcastServiceTests.cs` (rework); `tests/Dxs.Consigliere.Tests/Broadcast/BroadcastUnificationGrepTests.cs` (new) |
| `program-docs` | `repo-governance` | `docs/stream-tasks/broadcast-unification-wave/` |

### Handoff facts → consumers

| Consumer | Consumes | Allowed change | Forbidden without amendment |
|---|---|---|---|
| W6 production-ops | `IBroadcastService.BroadcastAsync` for admin-side replay / re-broadcast tooling; `BroadcastReceiptDto` shape unchanged | implement admin replay on top | rename `BroadcastAsync` |
| External wallets | new REST `POST /api/tx/broadcast` + SignalR `Broadcast(rawHex)` returning `BroadcastReceiptDto` | migrate client code | n/a (legacy is gone) |

## Slice Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | `consigliere-broadcast` (interface collapse design) | todo | — | new IBroadcastService compiles; the renamed `BroadcastAsync` signature compiles; no external entry points yet | `IBroadcastService` reduced to `SatoshisPerByte` + `BroadcastAsync`; existing P2P path migrated under the new name; unit test asserts the new shape | slice-A1 |
| S1 | `consigliere-hub-public` + `consigliere-rest-api` (entrypoint replacement) | todo | S0 | hub + controller tests resolve through DI; new endpoints return `BroadcastReceiptDto`; legacy routes return 404 / are gone | `WalletHub.Broadcast(rawHex)` returns `BroadcastReceiptDto`; `POST /api/tx/broadcast` (JSON body) returns receipt; `BroadcastTracked` deleted; `/api/tx/broadcast/{raw}` deleted | wave-A1 |
| S2 | `consigliere-broadcast` (legacy method delete) | todo | S0, S1 | `dotnet build` green; no `Broadcast(string)` / `Broadcast(Transaction)` symbols remain on `BroadcastService` | legacy methods + helpers deleted; ctor takes only P2P deps | wave-A1 |
| S3 | `bsv-runtime-ingest` (bitcoind broadcast + IBroadcastProvider) | todo | S2 | `BitcoindService.Broadcast` deleted; `IBroadcastProvider` interface deleted; `BitcoindService` no longer implements it (keeps `SatoshisPerByte`); `dotnet build` green | broadcast-side of bitcoind gone; `IBroadcastProvider` symbol removed | wave-A1 |
| S4 | `external-providers` (Bitails / WoC client cleanup) | todo | S2 | `IBitailsRestApiClient.Broadcast` deleted + impl deleted + DTO deleted; `IWhatsOnChainRestApiClient.BroadcastAsync` deleted + impl deleted; `dotnet build` green | grep for `Broadcast` / `BroadcastAsync` on those interfaces returns no matches | wave-A1 |
| S5 | `program-tests` (test rework) | todo | S1, S2, S3, S4 | `BroadcastServiceTests` recompiles; new P2P-focused tests green; grep regression test (S6 dep) consumes the cleanup | legacy `Broadcast_*` tests removed; 4 new `BroadcastAsync_*` tests added; coverage on policy / persist / announce / no-ready-peer | wave-A1 |
| S6 | `program-tests` (grep + DI regression) | todo | S1-S5 | `BroadcastUnificationGrepTests` green; DI regression test green; admin endpoint integration test green | grep regression test enforces "no legacy" at the build level; admin controller test covers the new route | wave-A1 |
| S7 | live mainnet validation (operator-driven) | todo (deferrable) | S0-S6 | operator submits a real mainnet tx via the new endpoint; confirms block-inclusion within 2-3 blocks; receipt state transitions Validated → Broadcasted → Confirmed via SignalR `OnBroadcastStateChanged` | one observed mainnet broadcast recorded in `evidence/live-validation.md` | wave-A1 |

S0 (interface collapse) is the **prerequisite slice** required by
the program launch rule. Its slice-level audit gates S1+ open.
S1-S6 covered by `audits/wave5-audit-A1.md` after they all close.
S7 may close after the audit if operator-deferred (W2 S8 / W3 S7
/ W4 S8 pattern).

## Definition of Done

- All slices `done` (S7 may be operator-deferred with rationale).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W5 baseline
  (3 pre-existing Raven embedded-runtime failures unchanged from
  W4 close).
- Grep regression test green:
  `BroadcastUnificationGrepTests.NoLegacyBroadcastCallsRemain`.
- DI regression test green: `IBroadcastService` resolves with
  the collapsed ctor signature.
- Admin endpoint reachable + returns `BroadcastReceiptDto` shape
  (controller test).
- Wave-level Codex audit at `audits/wave5-audit-A1.md` returns
  APPROVE (or APPROVE WITH CHANGES addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice, plus
  the wallet-client migration snippet for external consumers.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: `0e87265` (initial draft + A1 audit prompt)
- Wave audit A1: pending (Codex prompt staged for user; in-flight
  implementation continues in parallel)
- Slice S0 delivery: `8a999d9` (SubmitAsync → BroadcastAsync rename
  + shape pin test)
- Slices S1 + S2 + S3 + S4 + S5 + S6 delivery: this commit
  (entrypoint replacement, BroadcastService collapse, IBroadcastProvider
  → IFeeRateProvider rename, Bitails / WoC client cleanup, legacy test
  removal, grep regression suite)
- Slice S7: deferred — operator-driven live mainnet validation
- Wave closeout evidence: `evidence/closeout.md`
- Wave audit A2 (post-execution): folded in `1592fdb` (8/8
  findings closed — H1 no-ready-peer Dispatching fix +
  M1 5 behavioral tests via IBroadcastPolicyValidator +
  IOutgoingTransactionRepository abstractions + M2 production
  DI graph test + L1-L5 cleanup)
- Wave audit A2-followup (APPROVE WITH CHANGES — 7 closed, 1
  partial, 2 new LOW): folded in this commit. M1 duplicate test
  added; M2 class-comment fixed; N1 master.md ctor-drift
  corrected; N2 nullable annotations fixed. Wave CLOSED.
