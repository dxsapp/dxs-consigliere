# thin-node S2 — slice-audit prompt

Audit target: S2 (P2P rawTx fetch + external auto-fallback).
Diff: `1c2ccda..8d65a1b`.

Read `master.md` Product Decision 1 + Core Rule 2, `slices.md` §S2.

## What landed
- `IP2pRawTransactionClient.TryGetRawAsync(txId, ct) : Task<byte[]?>`.
- `P2pRawTransactionClient`: broadcasts `getdata(MSG_TX, txid)` to every
  Ready peer, one-shot `tx`-frame handler per peer via
  `PerSessionDispatcherRegistry`, returns the first frame whose sha256d
  matches the requested txid; null on timeout / no ready peers /
  `!Enabled` / `!Bound` / malformed txid. Bounded by
  `BsvP2pConfig.RawTxFetchTimeoutMs` (default 3000ms).
- `RawTransactionFetchService`: `p2p` switch case + injected client.
- Registered singleton in `BsvP2pSetup`.

## Focus
1. **Null-on-miss, never throw (Core Rule 2).** Verify every miss path
   (timeout, notfound, no ready peers, subsystem off, hash mismatch)
   returns null so the fetch-service loop reaches external fallback.
   A throw here aborts fallback — that would be a C-level bug.
2. **Txid verification.** The returned tx's sha256d must equal the
   requested txid (wire-order). Confirm no unverified bytes can be
   returned (cache-poisoning / wrong-tx risk).
3. **Peer fan-out cleanup.** Broadcasting `getdata` to all Ready peers +
   per-peer one-shot handlers: confirm handlers/subscriptions are torn
   down in `finally` (no dispatcher leak per peer per fetch); the linked
   CTS is disposed.
4. **No impact on broadcast.** `TxRelayCoordinator`'s getdata serving /
   relay path must be unaffected by the new on-demand fetch sharing the
   dispatcher.

## Validation evidence
- `dotnet build … -c Release` clean.
- `RawTransactionFetchServiceTests` 6/6: p2p hit → Provider=="p2p";
  p2p null → junglebus fallback returns its bytes; all-fail → throws.

## Verdict + finding format
Verdict first; findings `C*|H*|M*|L*` with file:line, why, fix.
