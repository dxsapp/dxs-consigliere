# Slices — transaction lab

Source of truth: `master.md`. Co-author trailer on commits.
On ambiguity: smallest safe call, document in A1, continue.

## Anchors (existing, reuse)
- Broadcast: `POST /api/tx/broadcast` body `{ rawHex }` →
  `BroadcastReceiptDto` (`TransactionController.cs` ~L110; admin cookie →
  `BroadcastSource.Operator`).
- UTXOs: `GET /api/address/{address}/utxos` (`AddressController.cs` ~L75).
- Track: `POST /api/admin/tracked/addresses` `{address,name?,historyMode?}`
  (shipped 12a9518) + `admin-client.trackAddress`.
- SDK: `dxs-bsv-token-sdk@1.0.4` (npm) — built in-house; P2PKH keygen + tx
  build/sign. Inspect its exports after install to use the real API
  (key generation, address derivation, P2PKH tx build + sign to rawHex).
- Frontend conventions: `screens/p2p/p2p.store.ts` (store idiom),
  `screens/addresses/AddressesPage.tsx` (MUI form/list), `app/App.tsx`
  (lazy route pattern), `lib/admin/admin-client.ts`, `lib/api/routes.ts`.

## S1 — lab screen (client-side keygen / sign / broadcast)

**Owned:** `src/admin-ui` only — `package.json` (add SDK), new `screens/lab/`
(`LabPage.tsx` + `lab.store.ts` + a unit test), `lib/admin/admin-client.ts`
(+ `getAddressUtxos(address)` and `broadcastRawTx(rawHex)` helpers — note
`/api/address/...` + `/api/tx/broadcast` are non-admin routes but same-origin;
add path builders in `lib/api/routes.ts`), `app/App.tsx` (+ nav entry).

**Task:**
1. `pnpm --dir src/admin-ui add dxs-bsv-token-sdk@^1.0.4`. Read its exports /
   types to learn the real API (do NOT guess method names — inspect
   `node_modules/dxs-bsv-token-sdk`). If it's ESM/needs Vite config, wire it.
2. `lab.store.ts` (p2p.store idiom): `generate()` → new P2PKH keypair, derive
   mainnet address, keep WIF in memory only; immediately `admin.trackAddress({
   address, name: "lab", historyMode: "forward_only" })` so it's watched.
   `refresh()` → `getAddressUtxos(address)` + derive balance. `send(dest,
   sats)` → select UTXOs + build + sign a P2PKH tx with the SDK (change back
   to the lab address, simple fee) → `broadcastRawTx(rawHex)` → store the
   receipt. Surface errors (insufficient funds, broadcast 400).
3. `LabPage.tsx`: "Generate key" (shows address + a reveal-WIF affordance +
   copy), balance/UTXO panel with refresh, simple-send form (destination +
   amount sats), broadcast result (txid + link to `/addresses/:address`).
   Clear lab/mainnet warning banner.
4. Route `/lab` (lazy/code-split) + a nav entry. Lazy-import the SDK on this
   route only if it's heavy (keep the shell bundle under budget).

**Not:** no backend changes; do NOT send the privkey/WIF to the backend; no
token txs; no full builder; types stay generated re-exports.

**Validation:** `pnpm --dir src/admin-ui verify` (incl. budget — watch the SDK
weight; lazy-load if needed); `RAVEN_URL=http://127.0.0.1:18080 pnpm --dir
src/admin-ui test:contract`; `bash scripts/secrets-lint.sh`. Unit test
(mock admin client): generate derives a stable address; send builds a rawHex
and calls broadcastRawTx with it; insufficient-funds path surfaces an error.
If the SDK can't run under jsdom/vitest, keep the build/sign behind a thin
wrapper and unit-test the store's orchestration with the wrapper mocked —
state what was tested.

## Closeout
Ledger done; hash in master Delivery Notes (separate backfill commit);
`audits/A1.md` + `evidence/closeout.md`; `audits/S1-slice-audit-prompt.md`.
