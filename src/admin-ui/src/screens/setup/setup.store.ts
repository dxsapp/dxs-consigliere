import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { SetupStatusResponse } from "@/types/admin";

/**
 * S10/A1 — read-only store backing the Setup screen.
 */
export type SetupStatus = "idle" | "loading" | "ready" | "error";

export class SetupStore {
  data: SetupStatusResponse | null = null;
  status: SetupStatus = "idle";
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
      const data = await this.admin.getSetupStatus(ctl.signal);
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
