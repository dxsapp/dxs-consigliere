# S8 live-mainnet validation — deferred

## Status

**Deferred to a follow-up operator session.** S0-S7 deliver the
full mempool-observer code path; S8 is the end-to-end "one watched
mainnet tx flows through to `WalletHub.OnTransactionFound`"
acceptance, which requires:

- a running mainnet Consigliere instance with
  `Consigliere:Broadcast:P2p:Enabled = true`
- a watched address pre-registered via `WatchingAddress` Raven docs
- a small live BSV payment to that address
- log collection across the inv → getdata → tx → match → journal →
  projection → hub correlation trail

These are operator-driven prerequisites that don't belong in the
code-review audit pass.

## Follow-up plan

- **Trigger.** Once Wave 2's wave-level Codex audit returns APPROVE
  (or APPROVE WITH CHANGES that don't block runtime), the operator
  schedules a 30-minute window with a small BSV transfer to a test
  address tracked in the mainnet instance.
- **Date.** TBD — depends on Wave 2 audit closure timing. Open
  window: any time within 2 weeks of audit approval; if it slips
  longer, re-open this evidence file with the reason.
- **Owner.** Operator (project author).
- **Evidence schema.** When the run completes, populate this file
  per `slices.md` §S8 required-fields table:
  - txid, watched_address
  - commit_sha, node_config_excerpt, peer_count_at_inv,
    peer_endpoint_inv_source
  - inv_arrival_ts_utc_ms, getdata_ts_utc_ms, tx_receipt_ts_utc_ms,
    match_ts_utc_ms, journal_append_ts_utc_ms, hub_fire_ts_utc_ms
  - inv_to_hub_delta_ms (pass condition: ≤ 2000 ms)
  - projection_doc_id, projection_last_seq
  - payload_available (pass condition: true)
  - raw_payload_reference
  - seen_by_sources (pass condition: contains "p2p")
  - bitails_also_observed, junglebus_also_observed

## Why a deferred S8 is safe to ship

The full path is already exercised in the test suite at fixture
level:

- **inv → matcher decision**: covered by
  `tests/Dxs.Bsv.Tests/P2p/Observer/MempoolWatcherTests.cs`
- **scriptSig pubkey → HASH160(pubkey) → matcher hit**: covered by
  `tests/Dxs.Bsv.Tests/P2p/Observer/TxScriptParserTests.cs` +
  `WatchlistFixtureSuiteTests.cs`
- **parsed tx → matcher → journal append + payload save**: covered
  by the S0 source-neutral journal overload tests + the S5 runner's
  `OnTxArrivedAsync` implementation
- **`SeenBySources` accumulates `p2p` + `bitails`**: covered by
  `tests/Dxs.Consigliere.Tests/Data/Transactions/SeenBySourcesProjectionTests.cs`
  (Raven-gated)

The remaining gap is the **integration timing** under real mainnet
load — which can only be measured by an actual mainnet run. The
audit-locked thresholds in `slices.md` §S8 keep that measurement
honest when it happens.

## Closure rule

This file's deferred status is part of the Wave 2 closeout. The
wave can be marked `done` with S8 explicitly deferred; the
follow-up evidence lands here when the operator session completes.
If the threshold fails on the live run, open a follow-up audit
slice rather than re-scoping Wave 2.
