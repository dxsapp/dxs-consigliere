# tx-lab S1 — slice-audit prompt

Target: S1 (/lab screen, client-side keygen/sign/broadcast). Diff: `6d03bf0..1ffe0e2`.
Read `master.md` + `slices.md` §S1.

## What landed (frontend only)
- `dxs-bsv-token-sdk@^1.0.4`, lazy-loaded into its own chunk (shell budget
  195.69/200 KB). Real API: `bsv.PrivateKey`, `bsv.Address.fromBase58`,
  `bsv.TransactionBuilder` (addInput → addP2PkhOutput → addChangeOutputWithFee
  → sign → toHex); WIF via the SDK's bs58check.
- `screens/lab/`: `lab.tx.ts` (mockable SDK wrapper), `lab.store.ts`
  (generate/refresh/send), `LabPage.tsx` (key + reveal/copy WIF, UTXO/balance,
  send form, receipt, mainnet/real-funds banner), store test.
- `admin-client`: `getAddressUtxos` (GET /api/address/{address}/utxos) +
  `broadcastRawTx` (POST /api/tx/broadcast) + mocks; reuses `trackAddress`.
- `/lab` lazy route + nav entry. No backend change.

## Focus (be adversarial)
1. **Key never leaves the browser.** Confirm ONLY the address (track) and the
   signed rawHex (broadcast) cross the wire — no WIF/priv in any request body
   or log. (Store test asserts this — re-check the actual client calls + the
   reveal-WIF UI doesn't POST it.)
2. **P2PKH correctness.** The builder signs against `UtxoDto.scriptPubKey`;
   change goes back to the lab address; fee is sane. A malformed/너무-small
   amount or insufficient funds surfaces an error and does NOT broadcast.
3. **Mainnet reality.** The lab builds REAL mainnet tx — banner present + clear.
   Address derivation is mainnet (fromBase58 mainnet-only).
4. **Bundle budget.** SDK is in a lazy chunk, not the shell (97.8% cap — close;
   confirm it didn't sneak into the shell).
5. **Auto-track.** Generated address is tracked forward_only on generate.

## Validation
`pnpm verify` green (205 unit, budget 195.69/200, contracts:check OK);
test:contract 24/24; secrets-lint 0. Store test: keygen→address+track,
send→rawHex→broadcast (no key material), insufficient-funds error. SDK
build/sign mocked under jsdom (real API verified by an install-time smoke).

## Residuals
- Live mainnet send (real funds, real broadcast) operator-run, evidence-pending.

Verdict + `C*|H*|M*|L*` findings.
