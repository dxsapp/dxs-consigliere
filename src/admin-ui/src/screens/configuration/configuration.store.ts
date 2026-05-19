import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminProvidersResponse } from "@/types/admin";

/**
 * S10/A1 — read-only store backing the Configuration page.
 * Mirrors the S5 detail-store contract: idempotent start, async
 * fetch, dispose aborts in-flight + flips a `disposed` guard so a
 * late resolution can't mutate observable state.
 *
 * Configuration + Providers + Setup all consume one of three small
 * stores; the Provider catalog is shared between Configuration and
 * Providers because both screens read `AdminProvidersResponse`.
 */
export type ConfigurationStatus = "idle" | "loading" | "ready" | "error";

export class ConfigurationStore {
  data: AdminProvidersResponse | null = null;
  status: ConfigurationStatus = "idle";
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
