import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminTrackedAddressResponse } from "@/types/admin";

/**
 * S5/A1 — Address detail store. One MobX store per screen
 * (Core Rule §4); the page is a render + lifecycle shell.
 *
 * Concurrent re-loads abort the prior request via AbortController.
 * `dispose()` aborts in-flight requests + drops any pending state.
 */
export type AddressDetailStatus = "idle" | "loading" | "ready" | "error";

export interface AddressDetailStoreOptions {
  admin: IAdminClient;
  address: string;
}

export class AddressDetailStore {
  data: AdminTrackedAddressResponse | null = null;
  status: AddressDetailStatus = "idle";
  error: string | null = null;

  readonly address: string;
  private readonly admin: IAdminClient;
  private inflight: AbortController | null = null;
  private disposed = false;

  constructor(opts: AddressDetailStoreOptions) {
    this.admin = opts.admin;
    this.address = opts.address;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    if (this.disposed) return;
    if (!this.address) {
      runInAction(() => {
        this.status = "error";
        this.error = "missing address";
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
      const data = await this.admin.getTrackedAddress(this.address, ctl.signal);
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
        this.error = err instanceof Error ? err.message : "Failed to load address";
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
