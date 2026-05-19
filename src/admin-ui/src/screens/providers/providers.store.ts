import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminProvidersResponse } from "@/types/admin";

/**
 * S10/A1 — read-only store backing the Providers capability matrix.
 * Kept as a sibling of `ConfigurationStore` (same payload) so the
 * one-store-per-screen rule from Core Rule §4 holds and so we don't
 * couple two screens to a single instance.
 */
export type ProvidersStatus = "idle" | "loading" | "ready" | "error";

export class ProvidersStore {
  data: AdminProvidersResponse | null = null;
  status: ProvidersStatus = "idle";
  error: string | null = null;

  private readonly admin: IAdminClient;
  private inflight: AbortController | null = null;

  constructor(opts: { admin: IAdminClient }) {
    this.admin = opts.admin;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.data ? "ready" : "loading";
      this.error = null;
    });
    try {
      const data = await this.admin.getProviders(ctl.signal);
      if (ctl.signal.aborted ) return;
      runInAction(() => {
        this.data = data;
        this.status = "ready";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted ) return;
      runInAction(() => {
        this.status = "error";
        this.error = err instanceof Error ? err.message : "Unknown error";
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  dispose(): void {
    this.inflight?.abort();
    this.inflight = null;
  }
}
