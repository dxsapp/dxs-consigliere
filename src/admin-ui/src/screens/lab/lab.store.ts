import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { BroadcastReceiptDto } from "@/types/admin";
import {
  buildP2pkhSend,
  generateLabKey,
  type BuildSendParams,
  type BuildSendResult,
  type LabKey,
  type LabUtxo,
} from "@/screens/lab/lab.tx";

/**
 * tx-lab S1 — Transaction Lab store. Mirrors the p2p.store idiom:
 * constructor takes the admin client, observable state + status, and a
 * per-action AbortController.
 *
 * The self-contained loop:
 *   generate() → new keypair in-browser → auto-track its address
 *   refresh()  → GET /api/address/{addr}/utxos → spendable balance
 *   send()     → select UTXOs → build+sign P2PKH (client-side) →
 *                broadcastRawTx(rawHex) → store the receipt
 *
 * CRITICAL: the private key / WIF NEVER leaves the browser. Only the
 * address (to track) and the signed `rawHex` (to broadcast) are sent
 * to the backend.
 *
 * The SDK build/sign calls are injected (defaults = the real wrapper)
 * so the orchestration can be unit-tested with the elliptic-curve SDK
 * mocked out — see lab.store.test.ts.
 */

export type LabStatus = "idle" | "loading" | "ready" | "error";

export interface LabStoreOptions {
  admin: IAdminClient;
  /** Injectable for tests — defaults to the real SDK wrapper. */
  generateKey?: () => Promise<LabKey>;
  buildSend?: (params: BuildSendParams) => Promise<BuildSendResult>;
}

export class LabStore {
  key: LabKey | null = null;
  utxos: LabUtxo[] = [];
  receipt: BroadcastReceiptDto | null = null;

  status: LabStatus = "idle";
  /** Independent flags so the UI can show per-action spinners. */
  generating = false;
  refreshing = false;
  sending = false;
  error: string | null = null;

  private readonly admin: IAdminClient;
  private readonly generateKeyFn: () => Promise<LabKey>;
  private readonly buildSendFn: (params: BuildSendParams) => Promise<BuildSendResult>;
  private inflight: AbortController | null = null;

  constructor(opts: LabStoreOptions) {
    this.admin = opts.admin;
    this.generateKeyFn = opts.generateKey ?? generateLabKey;
    this.buildSendFn = opts.buildSend ?? buildP2pkhSend;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  dispose(): void {
    this.inflight?.abort();
    this.inflight = null;
  }

  /** Total spendable balance (sats) across the loaded UTXO set. */
  get balanceSats(): number {
    return this.utxos.reduce((sum, u) => sum + u.satoshis, 0);
  }

  get address(): string | null {
    return this.key?.address ?? null;
  }

  /**
   * Generate a fresh keypair in the browser and immediately register
   * its address with the node so the loop is observable. The WIF stays
   * in `this.key` (memory only) — only the address is POSTed.
   */
  async generate(): Promise<void> {
    if (this.generating) return;
    runInAction(() => {
      this.generating = true;
      this.error = null;
    });
    try {
      const key = await this.generateKeyFn();
      // Auto-track so the node watches the lab address. Only the
      // address crosses the wire — never the WIF/private key.
      await this.admin.trackAddress({
        address: key.address,
        name: "lab",
        historyMode: "forward_only",
      });
      runInAction(() => {
        this.key = key;
        this.utxos = [];
        this.receipt = null;
        this.status = "ready";
      });
      await this.refresh();
    } catch (err) {
      runInAction(() => {
        this.status = "error";
        this.error = errMessage(err);
      });
    } finally {
      runInAction(() => {
        this.generating = false;
      });
    }
  }

  /** Reload the lab address's UTXO set + balance. */
  async refresh(): Promise<void> {
    const address = this.key?.address;
    if (!address) return;
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.refreshing = true;
      this.error = null;
    });
    try {
      const res = await this.admin.getAddressUtxos(address, ctl.signal);
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.utxos = (res.utxoSet ?? [])
          .filter((u) => u.scriptPubKey && u.txId)
          .map((u) => ({
            txId: u.txId as string,
            vout: u.vout,
            satoshis: u.satoshis,
            scriptPubKey: u.scriptPubKey as string,
          }));
        this.status = "ready";
      });
    } catch (err) {
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.status = "error";
        this.error = errMessage(err);
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
      runInAction(() => {
        this.refreshing = false;
      });
    }
  }

  /**
   * Build + sign a P2PKH send client-side, then broadcast the raw hex.
   * Change returns to the lab address. Surfaces insufficient-funds and
   * broadcast (400 body) errors.
   */
  async send(destination: string, amountSats: number): Promise<void> {
    if (this.sending) return;
    const key = this.key;
    if (!key) {
      runInAction(() => {
        this.error = "Generate a key first.";
      });
      return;
    }
    if (!destination.trim()) {
      runInAction(() => {
        this.error = "Destination address is required.";
      });
      return;
    }
    if (!Number.isFinite(amountSats) || amountSats <= 0) {
      runInAction(() => {
        this.error = "Amount must be a positive number of satoshis.";
      });
      return;
    }

    runInAction(() => {
      this.sending = true;
      this.error = null;
      this.receipt = null;
    });
    try {
      const { rawHex } = await this.buildSendFn({
        fromWif: key.wif,
        destination,
        amountSats,
        utxos: this.utxos,
      });
      const receipt = await this.admin.broadcastRawTx(rawHex);
      runInAction(() => {
        this.receipt = receipt;
        this.status = "ready";
      });
      // Refresh the UTXO set so the spent coin disappears.
      await this.refresh();
    } catch (err) {
      runInAction(() => {
        this.status = "error";
        this.error =
          err instanceof Error && err.message === "INSUFFICIENT_FUNDS"
            ? "Insufficient funds: balance can't cover the amount plus fee."
            : errMessage(err);
      });
    } finally {
      runInAction(() => {
        this.sending = false;
      });
    }
  }
}

function errMessage(err: unknown): string {
  if (err instanceof Error) return err.message;
  if (typeof err === "object" && err && "message" in err) {
    return String((err as { message: unknown }).message);
  }
  return "Unknown error";
}
