---
created: 2026-05-30
type: wave
parent: docs/stream-tasks/consigliere-thin-node-observer-program/
related: docs/stream-tasks/thin-node-primary-source-wave/ (P2P broadcast path);
         12a9518 (track-a-new address UI); src/Dxs.Consigliere/Controllers/TransactionController.cs (POST /api/tx/broadcast)
status: done (S1 shipped; live mainnet send operator-run)
---

# Transaction lab — generate key, fund, send, broadcast

A built-in "lab" admin screen to demonstrate the full self-contained loop:
generate a key in the browser → its address is auto-tracked → fund it → build
+ sign a simple P2PKH send → broadcast over the node's own P2P pool → watch it
confirm. For a demo video showing Consigliere needs no third-party
infrastructure.

## Product Decision

1. **Client-side keys + signing.** The keypair is generated in the browser and
   the transaction is built + signed in-browser with `dxs-bsv-token-sdk`
   (npm, v1.0.4). The private key never leaves the client; the backend only
   broadcasts the finished raw tx. This is a LAB tool — keys are demo-grade
   (held in browser memory/session, not custodial storage).
2. **v1 = simple P2PKH send.** One form: send N satoshis to a destination
   address, change back to the lab address, auto UTXO selection + fee. No
   multi-output / OP_RETURN / manual UTXO picking yet.
3. **Reuse existing backend — no backend changes.** UTXOs come from
   `GET /api/address/{address}/utxos`; broadcast via `POST /api/tx/broadcast`
   `{rawHex}` (admin cookie → `BroadcastSource.Operator`); auto-track via the
   `POST /api/admin/tracked/addresses` endpoint shipped in `12a9518`.
4. **Mainnet, clearly labelled.** The product is mainnet; the lab builds REAL
   transactions. The screen is labelled a lab/demo tool and warns funds are
   real — keep amounts small.

## Scope

In scope (frontend only):
- Add `dxs-bsv-token-sdk@^1.0.4` to `src/admin-ui`.
- New `/lab` admin screen + store: generate P2PKH keypair (show address +
  WIF), auto-track the address, poll balance/UTXOs, simple-send form,
  build+sign (SDK) → broadcast → show the `BroadcastReceiptDto` + a link to
  the tx/address detail. Nav entry + route.
- `admin-client` helpers for `GET /api/address/{address}/utxos` and
  `POST /api/tx/broadcast` (or call via the api client directly).

Out of scope:
- Any backend change (broadcast + utxo + track endpoints already exist).
- Token (DSTAS/native) transactions — P2PKH only in v1.
- Custodial key storage / server-side signing.
- Full tx builder (multi-output, manual UTXO, data outputs).

## Core Rules

1. **Private key stays client-side.** Never POST the privkey/WIF to the
   backend; only the signed `rawHex` is sent to `/api/tx/broadcast`.
2. **Reuse, don't rebuild.** No new broadcast/utxo/track endpoints.
3. **Lab labelling.** The screen states it is a lab tool on mainnet with real
   funds; small amounts.
4. **Bundle budget.** The SDK + screen must be code-split/lazy so the shell
   bundle budget check stays green; if the SDK is heavy, lazy-import it only
   on the lab route.
5. **Gates.** `pnpm verify` (typecheck/lint/test/build/budget/inventory/
   contracts:check) + `pnpm test:contract` + `secrets-lint`. Types stay
   generated re-exports.

## Ownership Zones

- `lab-frontend` — `src/admin-ui` only: `package.json` (SDK dep), new
  `screens/lab/`, `lib/admin/admin-client.ts` (utxo + broadcast helpers),
  `lib/api/routes.ts`, `app/App.tsx` (route) + nav. (S1)

## Wave Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S1 | `lab-frontend` | **done** | — (backend endpoints already exist) | `pnpm verify` + `pnpm test:contract` + `secrets-lint`; store unit test: keygen → address derived; build+sign produces a valid rawHex (SDK); broadcast called with rawHex (mock) | `/lab` screen: generate key → auto-track → show UTXOs/balance → simple P2PKH send → sign client-side → broadcast; privkey never sent to backend; bundle budget green | `audits/S1-slice-audit-prompt.md` |

## Definition of Done

- From `/lab`: generate a key (address auto-tracked), fund it externally, send
  a P2PKH tx that the node broadcasts over P2P, see the receipt — all without
  any third-party provider.
- Private key never leaves the browser.
- `pnpm verify` + `pnpm test:contract` + `secrets-lint` green; bundle budget
  under cap.
- `audits/A1.md` + `evidence/closeout.md`.

## Delivery Notes

| slice | commit | summary |
|---|---|---|
| S1 | `1ffe0e2` | lab screen: client-side keygen/sign + P2P broadcast |
| Audit folds | none | per-slice audit prompt written; no findings folded |
