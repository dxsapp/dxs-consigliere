import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminTrackedTokenResponse } from "@/types/admin";

/**
 * S5/A1 — Token detail store. Mirrors AddressDetailStore exactly;
 * factored as a sibling rather than a generic so the type
 * signatures stay legible at the page boundary.
 */
export type TokenDetailStatus = "idle" | "loading" | "ready" | "error";

export interface TokenDetailStoreOptions {
  admin: IAdminClient;
  tokenId: string;
}

export class TokenDetailStore {
  data: AdminTrackedTokenResponse | null = null;
  status: TokenDetailStatus = "idle";
  error: string | null = null;

  readonly tokenId: string;
  private readonly admin: IAdminClient;
  private inflight: AbortController | null = null;
  private disposed = false;

  constructor(opts: TokenDetailStoreOptions) {
    this.admin = opts.admin;
    this.tokenId = opts.tokenId;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    if (this.disposed) return;
    if (!this.tokenId) {
      runInAction(() => {
        this.status = "error";
        this.error = "missing tokenId";
      });
      return;
    }
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.data ? "ready" : "loading";
      this.error = null;
    });
    try {
      const data = await this.admin.getTrackedToken(this.tokenId, ctl.signal);
      if (ctl.signal.aborted || this.disposed) return;
      runInAction(() => {
        this.data = data;
        this.status = "ready";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted || this.disposed) return;
      runInAction(() => {
        this.status = "error";
        this.error = err instanceof Error ? err.message : "Failed to load token";
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  dispose(): void {
    this.disposed = true;
    this.inflight?.abort();
    this.inflight = null;
  }
}
