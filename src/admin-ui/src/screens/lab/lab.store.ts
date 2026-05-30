import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
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
 *   generate()      → new keypair in-browser → auto-track its address
 *   refresh()       → GET /api/address/{addr}/utxos → spendable balance
 *   createAndSign() → select UTXOs → build+sign P2PKH (client-side) →
 *                     store the raw hex for the operator to copy
 *
 * The lab deliberately does NOT broadcast: it only builds + signs and
 * surfaces the raw tx hex. The operator copies it into the Broadcast
 * inspector (POST /api/tx/broadcast) and watches the round-trip there —
 * a tx is only "executed" once peers accept it and relay it back, which
 * the node's own P2P observer then credits to this address (the balance
 * updates on the next auto-refresh). Keeping build/sign and broadcast as
 * two explicit steps is clearer for a demo.
 *
 * CRITICAL: the private key / WIF is never sent to the BACKEND. Only the
 * address (to track) crosses the wire from here; the signed `rawHex` is
 * shown for the operator to broadcast. For lab continuity the keypair IS
 * persisted to `localStorage` (browser-local) so a page refresh doesn't
 * lose the wallet — these are demo-grade keys (the page banner says so);
 * use "Reset wallet" to wipe.
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
  /** Balance auto-refresh interval (ms). Default 6s. */
  pollMs?: number;
  /** Injectable timers (tests pass no-ops to avoid real intervals). */
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

const DEFAULT_POLL_MS = 6_000;

export class LabStore {
  key: LabKey | null = null;
  utxos: LabUtxo[] = [];
  /** The last built+signed raw tx hex, for the operator to copy into the
   *  Broadcast inspector. Null until createAndSign() succeeds. */
  signedHex: string | null = null;

  status: LabStatus = "idle";
  /** Independent flags so the UI can show per-action spinners. */
  generating = false;
  refreshing = false;
  building = false;
  error: string | null = null;

  private readonly admin: IAdminClient;
  private readonly generateKeyFn: () => Promise<LabKey>;
  private readonly buildSendFn: (params: BuildSendParams) => Promise<BuildSendResult>;
  private readonly pollMs: number;
  private readonly setIntervalFn: NonNullable<LabStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<LabStoreOptions["clearInterval"]>;
  private timer: ReturnType<typeof setInterval> | null = null;
  private inflight: AbortController | null = null;

  constructor(opts: LabStoreOptions) {
    this.admin = opts.admin;
    this.generateKeyFn = opts.generateKey ?? generateLabKey;
    this.buildSendFn = opts.buildSend ?? buildP2pkhSend;
    this.pollMs = opts.pollMs ?? DEFAULT_POLL_MS;
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    // Restore a previously-generated lab wallet so a page refresh keeps
    // the address + spend ability (see the class-doc note on persistence).
    this.key = loadWallet();
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /** Call on mount: if a wallet was restored from localStorage, load its
   *  UTXOs and start auto-refreshing so the balance updates on its own
   *  after the address is funded (no manual Refresh needed). */
  async start(): Promise<void> {
    if (!this.key) return;
    this.startPolling();
    await this.refresh();
  }

  dispose(): void {
    this.stopPolling();
    this.inflight?.abort();
    this.inflight = null;
  }

  /** Wipe the lab wallet from memory + localStorage. */
  reset(): void {
    this.stopPolling();
    this.inflight?.abort();
    this.inflight = null;
    this.key = null;
    this.utxos = [];
    this.signedHex = null;
    this.error = null;
    this.status = "idle";
    clearWallet();
  }

  private startPolling(): void {
    if (this.timer) return;
    this.timer = this.setIntervalFn(() => void this.refresh(), this.pollMs);
  }

  private stopPolling(): void {
    if (this.timer) this.clearIntervalFn(this.timer);
    this.timer = null;
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
      saveWallet(key);
      runInAction(() => {
        this.key = key;
        this.utxos = [];
        this.signedHex = null;
        this.status = "ready";
      });
      this.startPolling();
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
   * Build + sign a P2PKH send entirely client-side and store the raw hex.
   * Change returns to the lab address. Does NOT broadcast — the operator
   * copies `signedHex` into the Broadcast inspector. Surfaces
   * insufficient-funds errors.
   */
  async createAndSign(destination: string, amountSats: number): Promise<void> {
    if (this.building) return;
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
      this.building = true;
      this.error = null;
      this.signedHex = null;
    });
    try {
      const { rawHex } = await this.buildSendFn({
        fromWif: key.wif,
        destination,
        amountSats,
        utxos: this.utxos,
      });
      runInAction(() => {
        this.signedHex = rawHex;
        this.status = "ready";
      });
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
        this.building = false;
      });
    }
  }
}

const LAB_WALLET_KEY = "consigliere.lab.wallet";

/** Browser-local persistence of the lab keypair (demo-grade keys; lab
 *  continuity across refresh). All access is guarded so SSR / disabled
 *  storage / malformed JSON degrade to "no wallet" rather than throwing. */
function loadWallet(): LabKey | null {
  try {
    const raw = globalThis.localStorage?.getItem(LAB_WALLET_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<LabKey>;
    if (
      parsed &&
      typeof parsed.address === "string" &&
      typeof parsed.wif === "string" &&
      typeof parsed._privHex === "string"
    ) {
      return { address: parsed.address, wif: parsed.wif, _privHex: parsed._privHex };
    }
  } catch {
    /* unavailable / malformed — treat as no wallet */
  }
  return null;
}

function saveWallet(key: LabKey): void {
  try {
    globalThis.localStorage?.setItem(LAB_WALLET_KEY, JSON.stringify(key));
  } catch {
    /* storage unavailable — wallet stays in memory only */
  }
}

function clearWallet(): void {
  try {
    globalThis.localStorage?.removeItem(LAB_WALLET_KEY);
  } catch {
    /* nothing to do */
  }
}

function errMessage(err: unknown): string {
  if (err instanceof Error) return err.message;
  if (typeof err === "object" && err && "message" in err) {
    return String((err as { message: unknown }).message);
  }
  return "Unknown error";
}
