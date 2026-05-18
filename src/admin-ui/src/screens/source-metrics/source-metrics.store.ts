import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  SourceKey,
  SourceMetricsResponse,
  SourceObservationCounters,
} from "@/types/admin";

/**
 * S9 — Source metrics store. Polls `/api/admin/metrics/sources`
 * + derives per-source first-seen-delta sparklines from the
 * snapshot history. Mirrors the dashboard's compute but scoped
 * to a per-source view.
 */
export interface SourceMetricsStoreOptions {
  admin: IAdminClient;
  pollMs?: number;
  lastN?: number;
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

const DEFAULT_POLL_MS = 30_000;
const DEFAULT_LAST_N = 24;

export class SourceMetricsStore {
  metrics: SourceMetricsResponse | null = null;
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;

  private readonly admin: IAdminClient;
  private readonly pollMs: number;
  private readonly lastN: number;
  private readonly setIntervalFn: NonNullable<SourceMetricsStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<SourceMetricsStoreOptions["clearInterval"]>;
  private timer: ReturnType<typeof setInterval> | null = null;
  private inflight: AbortController | null = null;
  private disposed = false;

  constructor(opts: SourceMetricsStoreOptions) {
    this.admin = opts.admin;
    this.pollMs = opts.pollMs ?? DEFAULT_POLL_MS;
    this.lastN = opts.lastN ?? DEFAULT_LAST_N;
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    if (this.disposed || this.timer) return;
    await this.refresh();
    this.timer = this.setIntervalFn(() => void this.refresh(), this.pollMs);
  }

  dispose(): void {
    this.disposed = true;
    if (this.timer) this.clearIntervalFn(this.timer);
    this.timer = null;
    this.inflight?.abort();
    this.inflight = null;
  }

  async refresh(): Promise<void> {
    if (this.disposed) return;
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.metrics ? "ready" : "loading";
    });
    try {
      const res = await this.admin.getSourceMetrics({
        lastN: this.lastN,
        signal: ctl.signal,
      });
      if (ctl.signal.aborted || this.disposed) return;
      runInAction(() => {
        this.metrics = res;
        this.status = "ready";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted || this.disposed) return;
      runInAction(() => {
        this.status = "error";
        this.error = err instanceof Error ? err.message : "Unknown error";
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  firstSeenSeries(src: SourceKey): number[] {
    const hist = this.metrics?.history ?? [];
    if (hist.length < 2) return [];
    const out: number[] = [];
    for (let i = 1; i < hist.length; i++) {
      const a = hist[i - 1].visibilityCounters[src]?.firstSeen ?? 0;
      const b = hist[i].visibilityCounters[src]?.firstSeen ?? 0;
      out.push(Math.max(0, b - a));
    }
    return out;
  }

  latestForSource(src: SourceKey): SourceObservationCounters | null {
    return this.metrics?.latest?.observationCounters[src] ?? null;
  }
}
