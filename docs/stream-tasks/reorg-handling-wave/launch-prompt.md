# Launch — Wave 3: Reorg Handling

## Mission

Detect blockchain re-organisations from the W1 headers chain,
journal `BlockObservation(Disconnected)` events for each
orphaned block, transition affected projection rows to
`Reorged`, emit the W1-frozen `IWalletHub.OnReorg(ReorgEventDto)`
to subscribed SignalR clients, and actively re-broadcast any
orphaned transactions for which we hold raw bytes so wallets
don't silently lose payments after a fork resolves.

End state at wave close:

- `BlockObservationJournalWriter.AppendDisconnectedAsync(
  blockHash, source, reason?, ct)` lands with idempotent
  fingerprint `block.disconnected:{blockHash}:{source}` and
  `IsDuplicate` propagation (parallel to W2 S0-A1 H1 fix).
- `ReorgDetector` (`src/Dxs.Bsv/P2p/Chain/`) consumes
  `HeadersChain.TryExtend → ExtendResult.Fork` results and
  returns `ReorgPlan { CommonAncestorHash,
  CommonAncestorHeight, OrphanedHashes, NewChainHashes,
  NewTipHash, NewTipHeight, IsDegraded }`. `IsDegraded = true`
  when the fork point falls below the 200-header retention
  window.
- `IOrphanedBlockBodyFetcher` (default impl
  `P2pOrphanedBlockBodyFetcher`) fetches orphaned block bodies
  via `getdata MSG_BLOCK` on a Ready peer, validates the merkle
  root against the stored header, returns
  `(BlockHeader header, IReadOnlyList<string> txIds)`. Caps
  `MaxFetchedBlockBytes` (256 MiB default), `BlockFetchTimeoutMs`
  (60 s default), `MaxBlockFetchRetries` (3). Cycles through
  Ready peers on timeout / mismatch.
- `ReorgEventEmitter` (`IHostedService`) drives the pipeline:
  fetch every orphan body → journal-append each Disconnected
  → fire one `OnReorg` to the `block:tip` SignalR group → hand
  the fetched tx lists to `OrphanedTxRebroadcaster`. Degraded
  reorgs skip fetch + journal and fire a single
  `OnReorg(DegradedState=true)`.
- `OrphanedTxRebroadcaster` looks up raw bytes (first
  `OutgoingTransactionStore`, then `IRawTransactionPayloadStore`)
  for each non-coinbase orphan tx and re-announces via the W2
  `TxRelayCoordinator.AnnounceAsync` primitive. Counters via
  `OrphanedTxRebroadcastRecorder`:
  `OrphanedTxAnnounced`,
  `OrphanedTxSkippedCoinbase`,
  `OrphanedTxSkippedNoRaw`,
  `OrphanedTxAnnounceNoReadyPeer`,
  `OrphanedTxAnnounceFailed`.
- E2E fixture suite green covering 1-deep / 2-deep / 5-deep
  forks, mismatched-body rejection, deep-reorg-beyond-window
  degraded path, idempotent replay, re-broadcast paths
  (outgoing / payload / coinbase / no-raw).
- DI regression test
  (`BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves`)
  resolves every W3 singleton from a mocked-external-deps
  service-provider (W2 A2-C1 fix pattern).
- One live-mainnet reorg observed and recorded in
  `evidence/live-validation.md` (operator-driven; may defer
  with explicit reason).

## Package path

`docs/stream-tasks/reorg-handling-wave/`
- `master.md` — goal, scope, ownership, slice ledger, definition of done
- `slices.md` — per-slice detail (S0-S7)
- `launch-prompt.md` — this file
- `audits/` — slice + wave audit reports
- `evidence/` — closeout, reorg-bench, live-validation

## Prerequisites

- Wave 1 closed (commit `f87da92` per program ledger). Provides
  `HeadersChain`, `IBlockHeaderStore`, frozen `ReorgEventDto`,
  frozen `IWalletHub.OnReorg`, `BlockObservation` shape.
- Wave 2 closed (commit `844b6e0` per program ledger). Provides
  `TxRelayCoordinator.AnnounceAsync`, `BsvP2pHealth`,
  `PerSessionDispatcherRegistry`, `IRawTransactionPayloadStore`,
  `OutgoingTransactionStore`,
  `BsvP2pSetupDiResolutionTests` pattern.

## Stop-and-audit protocol

Same as W1 / W2:

1. Wave package draft committed → request wave-level
   pre-execution audit (`audits/wave3-audit-A1.md`).
2. Address findings in-wave; commit revision.
3. S0 lands; request slice-A1 audit (`audits/S0-A1.md`); address
   findings; commit revision.
4. S1-S6 land in dependency order.
5. Wave-level post-execution audit
   (`audits/wave3-audit-A2.md`); address findings; commit
   revision.
6. Closeout (`evidence/closeout.md`); update parent program
   ledger; W3 row flipped to `done`.

S7 (live mainnet) may close after the wave audit if
operator-deferred — same pattern as W2 S8.

## Scope guards (do NOT silently expand)

- No new hub events or `PeerSession` callbacks. `OnReorg` is the
  only surface this wave touches. A per-tx `OnTransactionDeleted`
  event would require a contract-freeze amendment in
  `docs/stream-tasks/bsv-headers-chain-wave/` first.
- Retry queues for failed re-broadcasts → W6.
- Per-peer scoring on block-body fetch → W6.
- Multi-chain (BTC / BCH) generalisation → post-release backlog.
- No alterations to `TxLifecycleProjectionRebuilder` —
  W3 only produces the journal events; the rebuilder already
  consumes them correctly (W1 S5 of headers-chain-wave).

## Open scope questions for the audit

1. **Cumulative-work vs height for fork comparison.** The
   detector uses height as a proxy for cumulative work because
   `BlockHeader` does not carry per-header work today. Acceptable
   for the 200-header window?
2. **Idempotent-replay hub behaviour (S6 scenario 6).** Should
   the emitter suppress the second `OnReorg` when every journal
   append returns `IsDuplicate = true`, or fire it anyway and
   let clients de-dupe? Default proposal: fire anyway (clients
   handle).
3. **Per-tx `OnTransactionDeleted` event scope cut.** Confirm
   this stays out of W3 (route to W6) rather than amending the
   Wave 1 hub-contract freeze.
4. **Block-body fetch over P2P vs HTTP provider.** Default is
   P2P (`getdata MSG_BLOCK` on a Ready peer). A future wave can
   add an HTTP fallback. Acceptable for W3?
