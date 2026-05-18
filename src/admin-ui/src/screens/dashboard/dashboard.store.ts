import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { EventBus, Unsubscribe } from "@/lib/events/bus";
import type { P2pHealthDto, SourceMetricsResponse } from "@/types/admin";
import { SOURCE_KEYS, type SourceKey } from "@/types/admin";

/**
 * Dashboard store (S4 done-when). Per Core Rule §4:
 *  - one MobX store per screen, owned by `RootStore.dashboardStore`
 *    factories at S5+; here it's instantiated directly by the page
 *    for now.
 *  - subscribes to `OnBroadcastStateChanged` from the event bus +
 *    polls the admin REST endpoints.
 *  - exposes a fixed-size LRU of recent broadcasts (12 entries)
 *    + a 24-snapshot sparkline buffer for mempool rate + pool size.
 *  - `dispose()` unwires every subscription and timer (Core Rule §6).
 */
export interface RecentBroadcast {
  txId: string;
  state: string;
  updatedAtMs: number;
  failReason: string | null;
}

const RECENT_LIMIT = 12;
const SPARKLINE_LIMIT = 24;

export interface DashboardStoreOptions {
  admin: IAdminClient;
  bus: EventBus;
  /** Health polling interval in ms; default 15 s. */
  healthPollMs?: number;
  /** Metrics polling interval in ms; default 30 s. */
  metricsPollMs?: number;
  /** History depth to request for sparklines; default 24. */
  metricsLastN?: number;
  /** Test seam — overridable timer pair. Defaults to globalThis.setInterval. */
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

export class DashboardStore {
  /** REST-driven slices. */
  health: P2pHealthDto | null = null;
  metrics: SourceMetricsResponse | null = null;

  /** Async lifecycle state (Core Rule §5). Per-slice error fields
   *  so a metrics success doesn't blank a health failure (and vice
   *  versa); `lastError` is the union read the UI consumes. */
  healthStatus: "idle" | "loading" | "ready" | "error" = "idle";
  metricsStatus: "idle" | "loading" | "ready" | "error" = "idle";
  healthError: string | null = null;
  metricsError: string | null = null;

  /** Live data — populated via the event bus. */
  recentBroadcasts: RecentBroadcast[] = [];

  private readonly admin: IAdminClient;
  private readonly bus: EventBus;
  private readonly healthPollMs: number;
  private readonly metricsPollMs: number;
  private readonly metricsLastN: number;
  private readonly setIntervalFn: NonNullable<DashboardStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<DashboardStoreOptions["clearInterval"]>;

  private healthTimer: ReturnType<typeof setInterval> | null = null;
  private metricsTimer: ReturnType<typeof setInterval> | null = null;
  private disposers: Unsubscribe[] = [];
  private healthAbort: AbortController | null = null;
  private metricsAbort: AbortController | null = null;

  constructor(opts: DashboardStoreOptions) {
    this.admin = opts.admin;
    this.bus = opts.bus;
    this.healthPollMs = opts.healthPollMs ?? 15_000;
    this.metricsPollMs = opts.metricsPollMs ?? 30_000;
    this.metricsLastN = opts.metricsLastN ?? SPARKLINE_LIMIT;
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    makeAutoObservable(this, {}, { autoBind: true });
  }

  // ── Public lifecycle ─────────────────────────────────────────

  /** Boot the store. Idempotent — safe under StrictMode. */
  async start(): Promise<void> {
    if (this.disposers.length > 0) return;
    this.disposers.push(
      this.bus.on("OnBroadcastStateChanged", (evt) => this.recordBroadcast(evt))
    );
    await Promise.all([this.refreshHealth(), this.refreshMetrics()]);
    this.healthTimer = this.setIntervalFn(() => void this.refreshHealth(), this.healthPollMs);
    this.metricsTimer = this.setIntervalFn(() => void this.refreshMetrics(), this.metricsPollMs);
  }

  /** Tear down every poll + subscription. */
  dispose(): void {
    if (this.healthTimer) this.clearIntervalFn(this.healthTimer);
    if (this.metricsTimer) this.clearIntervalFn(this.metricsTimer);
    this.healthTimer = null;
    this.metricsTimer = null;
    this.healthAbort?.abort();
    this.metricsAbort?.abort();
    this.healthAbort = null;
    this.metricsAbort = null;
    for (const off of this.disposers) off();
    this.disposers = [];
  }

  // ── Derived data ─────────────────────────────────────────────

  /**
   * Per-source FirstSeen rate (events / minute) over the polled
   * history window. Empty array when the metrics haven't loaded yet.
   */
  get sourceRates(): Array<{ source: SourceKey; ratePerMinute: number }> {
    const hist = this.metrics?.history ?? [];
    if (hist.length < 2) return [];
    const oldest = hist[0];
    const newest = hist[hist.length - 1];
    const windowMin = (newest.snapshotUnixMs - oldest.snapshotUnixMs) / 60_000;
    if (windowMin <= 0) return [];
    return SOURCE_KEYS.map((src) => {
      const a = oldest.visibilityCounters[src]?.firstSeen ?? 0;
      const b = newest.visibilityCounters[src]?.firstSeen ?? 0;
      const delta = Math.max(0, b - a);
      return { source: src, ratePerMinute: Math.round((delta / windowMin) * 10) / 10 };
    });
  }

  /**
   * Mempool throughput sparkline — per-tick first-seen-delta summed
   * across all sources, oldest first.
   */
  get mempoolRateSeries(): number[] {
    const hist = this.metrics?.history ?? [];
    if (hist.length < 2) return [];
    const series: number[] = [];
    for (let i = 1; i < hist.length; i++) {
      let delta = 0;
      for (const src of SOURCE_KEYS) {
        const a = hist[i - 1].visibilityCounters[src]?.firstSeen ?? 0;
        const b = hist[i].visibilityCounters[src]?.firstSeen ?? 0;
        delta += Math.max(0, b - a);
      }
      series.push(delta);
    }
    return series;
  }

  /** Composite health summary fed to the hero card. */
  get healthSummary(): {
    status: "online" | "degraded" | "offline" | "unknown";
    label: string;
  } {
    if (!this.health) {
      return { status: "unknown", label: "loading…" };
    }
    if (!this.health.bound) {
      return { status: "offline", label: "Pool unbound" };
    }
    if (this.health.poolSize === 0) {
      return { status: "offline", label: "Pool empty" };
    }
    if (this.health.poolSize < this.health.targetPoolSize) {
      return { status: "degraded", label: `Pool ${this.health.poolSize}/${this.health.targetPoolSize}` };
    }
    return { status: "online", label: `Pool ${this.health.poolSize}/${this.health.targetPoolSize}` };
  }

  // ── Internal ─────────────────────────────────────────────────

  private recordBroadcast(evt: RecentBroadcast) {
    runInAction(() => {
      // Replace existing entry by txId so the newest state wins;
      // newest-first ordering with the LRU cap.
      const existing = this.recentBroadcasts.findIndex((b) => b.txId === evt.txId);
      const next: RecentBroadcast = { ...evt };
      const list =
        existing >= 0
          ? this.recentBroadcasts.filter((_, i) => i !== existing)
          : this.recentBroadcasts;
      this.recentBroadcasts = [next, ...list].slice(0, RECENT_LIMIT);
    });
  }

  private async refreshHealth(): Promise<void> {
    this.healthAbort?.abort();
    const ctl = new AbortController();
    this.healthAbort = ctl;
    runInAction(() => {
      this.healthStatus = this.health ? "ready" : "loading";
    });
    try {
      const res = await this.admin.getP2pHealth(ctl.signal);
      runInAction(() => {
        this.health = res;
        this.healthStatus = "ready";
        this.healthError = null;
      });
    } catch (err) {
      runInAction(() => {
        if (ctl.signal.aborted) return;
        this.healthStatus = "error";
        this.healthError = errorMessage(err);
      });
    }
  }

  private async refreshMetrics(): Promise<void> {
    this.metricsAbort?.abort();
    const ctl = new AbortController();
    this.metricsAbort = ctl;
    runInAction(() => {
      this.metricsStatus = this.metrics ? "ready" : "loading";
    });
    try {
      const res = await this.admin.getSourceMetrics({
        lastN: this.metricsLastN,
        signal: ctl.signal,
      });
      runInAction(() => {
        this.metrics = res;
        this.metricsStatus = "ready";
        this.metricsError = null;
      });
    } catch (err) {
      runInAction(() => {
        if (ctl.signal.aborted) return;
        this.metricsStatus = "error";
        this.metricsError = errorMessage(err);
      });
    }
  }

  /** Union view consumed by the hero's "backend unreachable" banner. */
  get lastError(): string | null {
    return this.healthError ?? this.metricsError;
  }
}

function errorMessage(err: unknown): string {
  if (err && typeof err === "object" && "message" in err && typeof err.message === "string") {
    return err.message;
  }
  return "Unknown error";
}
