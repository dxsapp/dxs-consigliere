import { makeAutoObservable, runInAction } from "mobx";
import { errorMessage } from "@/screens/addresses/addresses.store";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  AdminTrackTokenRequest,
  AdminTrackedTokenResponse,
} from "@/types/admin";

/**
 * Track-a-new — Tokens store. Symmetric with `AddressesStore`: list the
 * currently-tracked tokens + a small form to register a new one by
 * tokenId (+ optional symbol).
 *
 * History-mode judgment call: same as addresses — `forward_only` is
 * hardcoded (correct default for the P2P watchlist-filter test; no
 * backfill needed). Flip `HISTORY_MODE` to add full-history support.
 */
const HISTORY_MODE = "forward_only";

export interface TokensStoreOptions {
  admin: IAdminClient;
}

export class TokensStore {
  tokens: AdminTrackedTokenResponse[] = [];
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;

  // ── add-a-new form slice ──────────────────────────────────────
  formTokenId = "";
  formSymbol = "";
  submitting = false;
  submitError: string | null = null;

  private readonly admin: IAdminClient;
  private inflight: AbortController | null = null;

  constructor(opts: TokensStoreOptions) {
    this.admin = opts.admin;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    await this.refresh();
  }

  dispose(): void {
    this.inflight?.abort();
    this.inflight = null;
  }

  async refresh(): Promise<void> {
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.tokens.length ? "ready" : "loading";
    });
    try {
      const rows = await this.admin.getTrackedTokens(false, ctl.signal);
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.tokens = rows;
        this.status = "ready";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.status = "error";
        this.error = err instanceof Error ? err.message : "Unknown error";
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  setFormTokenId(v: string): void {
    this.formTokenId = v;
  }

  setFormSymbol(v: string): void {
    this.formSymbol = v;
  }

  get canSubmit(): boolean {
    return this.formTokenId.trim().length > 0 && !this.submitting;
  }

  async submit(): Promise<boolean> {
    const tokenId = this.formTokenId.trim();
    if (!tokenId || this.submitting) return false;
    runInAction(() => {
      this.submitting = true;
      this.submitError = null;
    });
    const req: AdminTrackTokenRequest = {
      tokenId,
      symbol: this.formSymbol.trim() || null,
      historyMode: HISTORY_MODE,
    };
    try {
      await this.admin.trackToken(req);
      runInAction(() => {
        this.formTokenId = "";
        this.formSymbol = "";
        this.submitting = false;
      });
      await this.refresh();
      return true;
    } catch (err) {
      runInAction(() => {
        this.submitting = false;
        this.submitError = errorMessage(err);
      });
      return false;
    }
  }
}
