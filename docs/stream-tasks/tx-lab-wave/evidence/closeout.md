# tx-lab — closeout

## Result
A `/lab` admin screen demonstrates the full self-contained loop with no
third-party provider: generate a key in the browser → address auto-tracked →
fund it → build+sign a simple P2PKH send client-side (`dxs-bsv-token-sdk`) →
broadcast over the node's own P2P pool → receipt. The private key never leaves
the browser. Built for the demo video to the BSV Association.

## Key files
- `src/admin-ui/src/screens/lab/` — `lab.tx.ts` (SDK wrapper), `lab.store.ts`,
  `LabPage.tsx`, store test.
- `src/admin-ui/src/lib/admin/admin-client.ts` — `getAddressUtxos` +
  `broadcastRawTx` (reuses existing endpoints; no backend change).
- `src/admin-ui/src/app/{App,routes}.tsx` — lazy `/lab` route + nav.

## Behavioral summary
- Generate → address auto-tracked (forward_only) → P2P watchlist filter picks
  it up live → fund → simple send → P2P broadcast → txid receipt.
- Client-side keys + signing; only address + signed rawHex cross the wire.
- Mainnet, clearly labelled as a lab tool with real funds (keep amounts small).

## Residuals
- Live mainnet send (real funds, broadcast + confirm) is operator-run — the
  actual video step.
- v1 = P2PKH only; tokens / full builder / custodial keys are out of scope.
- SDK lazy-chunked; shell bundle at 97.8% of the 200 KB cap.

## Delivery
| slice | commit | status |
|---|---|---|
| S1 lab screen | `1ffe0e2` | done |
