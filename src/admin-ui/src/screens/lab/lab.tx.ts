/**
 * tx-lab S1 — thin client-side crypto wrapper around
 * `dxs-bsv-token-sdk` (the in-house BSV SDK, npm `dxs-bsv-token-sdk`).
 *
 * Everything here runs ONLY in the browser tab. The private key is
 * generated here, the WIF is derived here, and the P2PKH transaction
 * is signed here. The store never hands any of this to the backend —
 * it only forwards the finished `rawHex` to `POST /api/tx/broadcast`.
 *
 * Why a wrapper:
 *  - It isolates the actual SDK surface in one place (so a future SDK
 *    rename is a one-file change), and
 *  - it gives the store a small, mockable seam so the orchestration
 *    can be unit-tested without booting the real elliptic-curve SDK
 *    under jsdom.
 *
 * Verified SDK API (v1.0.4, `dxs-bsv-token-sdk/bsv`):
 *   PrivateKey(pkBytes)               → .Address.Value/.Hash160, .PublicKey
 *   Address.fromBase58(str)           → mainnet-only, throws otherwise
 *   OutPoint(txId,vout,script,sats,addr,scriptType)
 *   ScriptType.p2pkh
 *   TransactionBuilder.init()
 *     .addInput(outPoint, privKey)
 *     .addP2PkhOutput(sats, toAddress)
 *     .addChangeOutputWithFee(changeAddress, change, satsPerByte)
 *     .sign().toHex()
 *   bs58check, fromHex  (WIF + script-hex helpers)
 */

/** A spendable coin as returned by `GET /api/address/{address}/utxos`. */
export interface LabUtxo {
  txId: string;
  vout: number;
  satoshis: number;
  /** Hex locking script (P2PKH) the SDK signs against. */
  scriptPubKey: string;
}

/** A freshly generated lab identity. The private key bytes never leave
 *  this object's lifetime — only `address` is sent to the backend (to
 *  track it); `wif` is shown in the UI for the operator. */
export interface LabKey {
  address: string;
  wif: string;
  /** Opaque handle the build step needs; holds the in-memory key. */
  readonly _privHex: string;
}

export interface BuildSendParams {
  fromWif: string;
  destination: string;
  amountSats: number;
  utxos: LabUtxo[];
  /** Simple flat fee rate; default chosen to clear typical relay min. */
  satsPerByte?: number;
}

export interface BuildSendResult {
  rawHex: string;
  /** Total input satoshis selected. */
  selectedSats: number;
  /** Number of inputs spent. */
  inputCount: number;
}

// 100 sat/kB = 0.1 sat/byte — the lab's flat fee rate.
const DEFAULT_SATS_PER_BYTE = 0.1;
const WIF_COMPRESSED_FLAG = 0x01;

/** Lazily loads the SDK so it lands in the lab route chunk, never the
 *  cold-load shell (keeps the shell bundle budget green). */
async function loadSdk() {
  const { bsv } = await import("dxs-bsv-token-sdk");
  return bsv;
}

/** Generate a fresh mainnet P2PKH keypair entirely in the browser. */
export async function generateLabKey(): Promise<LabKey> {
  const bsv = await loadSdk();
  const raw = crypto.getRandomValues(new Uint8Array(32));
  const pk = new bsv.PrivateKey(raw);
  const address = pk.Address.Value;

  // WIF (mainnet, compressed): 0x80 || 32-byte secret || 0x01.
  const wifPayload = new Uint8Array(34);
  wifPayload[0] = bsv.Networks.Mainnet.wif;
  wifPayload.set(raw, 1);
  wifPayload[33] = WIF_COMPRESSED_FLAG;
  const wif = bsv.bs58check.encode(wifPayload);
  const privHex = bsv.toHex(raw);

  return { address, wif, _privHex: privHex };
}

/** Decode a WIF string back into raw secret bytes (mainnet/compressed
 *  or uncompressed). Throws on a malformed/non-mainnet WIF. */
function wifToSecret(bsv: Awaited<ReturnType<typeof loadSdk>>, wif: string): Uint8Array {
  const decoded = bsv.bs58check.decode(wif);
  if (decoded[0] !== bsv.Networks.Mainnet.wif) {
    throw new Error("WIF is not a mainnet key");
  }
  // Strip the version byte and an optional trailing compression flag.
  const body = decoded.subarray(1);
  return body.length === 33 ? body.subarray(0, 32) : body;
}

/**
 * Build + sign a simple P2PKH send: spend enough UTXOs to cover
 * `amountSats` + fee, pay the destination, send change back to the
 * lab address. Returns the broadcast-ready `rawHex`.
 *
 * Throws `Error("INSUFFICIENT_FUNDS")` when the selected coins cannot
 * cover the amount plus the fee.
 */
export async function buildP2pkhSend(params: BuildSendParams): Promise<BuildSendResult> {
  const bsv = await loadSdk();
  const satsPerByte = params.satsPerByte ?? DEFAULT_SATS_PER_BYTE;

  const secret = wifToSecret(bsv, params.fromWif);
  const signer = new bsv.PrivateKey(secret);
  const fromAddress = signer.Address;
  const destination = bsv.Address.fromBase58(params.destination.trim());

  // Greedy UTXO selection: largest-first until we cover amount + a fee
  // headroom. The change output's fee is computed exactly by the SDK.
  const sorted = [...params.utxos].sort((a, b) => b.satoshis - a.satoshis);
  const builder = bsv.TransactionBuilder.init();
  let selectedSats = 0;
  let inputCount = 0;
  // Rough headroom so selection covers the eventual fee; the SDK does
  // the exact fee math in addChangeOutputWithFee.
  const target = params.amountSats + Math.ceil(satsPerByte * 200);

  for (const u of sorted) {
    const outPoint = new bsv.OutPoint(
      u.txId,
      u.vout,
      bsv.fromHex(u.scriptPubKey),
      u.satoshis,
      fromAddress,
      bsv.ScriptType.p2pkh
    );
    builder.addInput(outPoint, signer);
    selectedSats += u.satoshis;
    inputCount += 1;
    if (selectedSats >= target) break;
  }

  if (inputCount === 0 || selectedSats <= params.amountSats) {
    throw new Error("INSUFFICIENT_FUNDS");
  }

  builder.addP2PkhOutput(params.amountSats, destination);

  const change = selectedSats - params.amountSats;
  try {
    builder.addChangeOutputWithFee(fromAddress, change, satsPerByte);
  } catch {
    // The SDK throws when fee >= change (i.e. dust after the fee).
    throw new Error("INSUFFICIENT_FUNDS");
  }

  builder.sign();
  return { rawHex: builder.toHex(), selectedSats, inputCount };
}
