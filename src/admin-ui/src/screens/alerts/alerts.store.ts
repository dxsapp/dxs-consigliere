import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { P2pAlertEventDto } from "@/types/admin";

/**
 * S7 — Alerts store. Per A1 M1 the alerts feed is poll-delta, NOT
 * SignalR push. The store keeps a newest-first ordered list and an
 * `?since=` cursor so each refresh fetches only events strictly
 * after the previous tick.
 *
 * `newSinceLastTick` exposes the delta the page emits as a toast
 * (the page is responsible for the toast lifecycle; the store
 * just publishes the deltas).
 *
 * The active set is derived: `activeAlerts` are entries within the
 * `activeWindowMs` window; the rest is history.
 */
export interface AlertsStoreOptions {
  admin: IAdminClient;
  /** Poll interval. Default 15s. */
  pollMs?: number;
  /** Page size on each poll. Default 100. */
  lastN?: number;
  /** Window (in ms) within which an alert is considered active.
   *  Default 30 min. */
  activeWindowMs?: number;
  /** Test seam. */
  now?: () => number;
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

const DEFAULT_POLL_MS = 15_000;
const DEFAULT_LAST_N = 100;
const DEFAULT_ACTIVE_WINDOW_MS = 30 * 60 * 1000;

export class AlertsStore {
  alerts: P2pAlertEventDto[] = [];
  /** Newest alerts since the last poll tick. Caller consumes
   *  + clears via `consumeNewAlerts()`. */
  newSinceLastTick: P2pAlertEventDto[] = [];
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;
  /** Highest `alertUnixMs` we've seen — drives the `?since=` cursor. */
  cursor = 0;

  private readonly admin: IAdminClient;
  private readonly pollMs: number;
  private readonly lastN: number;
  private readonly activeWindowMs: number;
  private readonly nowFn: () => number;
  private readonly setIntervalFn: NonNullable<AlertsStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<AlertsStoreOptions["clearInterval"]>;
  private timer: ReturnType<typeof setInterval> | null = null;
  private inflight: AbortController | null = null;

  constructor(opts: AlertsStoreOptions) {
    this.admin = opts.admin;
    this.pollMs = opts.pollMs ?? DEFAULT_POLL_MS;
    this.lastN = opts.lastN ?? DEFAULT_LAST_N;
    this.activeWindowMs = opts.activeWindowMs ?? DEFAULT_ACTIVE_WINDOW_MS;
    this.nowFn = opts.now ?? (() => Date.now());
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    // S7-S12-audit M1 followup: idempotency is "timer set?"
    // (DashboardStore pattern), NOT a permanent `disposed` flag —
    // React StrictMode double-mounts effects, and a permanent
    // flag would lock the store after the first cleanup.
    if (this.timer) return;
    await this.refresh();
    this.timer = this.setIntervalFn(() => void this.refresh(), this.pollMs);
  }

  dispose(): void {
    if (this.timer) this.clearIntervalFn(this.timer);
    this.timer = null;
    this.inflight?.abort();
    this.inflight = null;
  }

  /** Consume the delta — the page reads + clears in one shot to
   *  avoid double-toasting on rerender. */
  consumeNewAlerts(): P2pAlertEventDto[] {
    const out = this.newSinceLastTick;
    runInAction(() => {
      this.newSinceLastTick = [];
    });
    return out;
  }

  get activeAlerts(): P2pAlertEventDto[] {
    const cutoff = this.nowFn() - this.activeWindowMs;
    return this.alerts.filter((a) => a.alertUnixMs >= cutoff);
  }

  get historyAlerts(): P2pAlertEventDto[] {
    const cutoff = this.nowFn() - this.activeWindowMs;
    return this.alerts.filter((a) => a.alertUnixMs < cutoff);
  }

  get activeCount(): number {
    return this.activeAlerts.length;
  }

  async refresh(): Promise<void> {
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.alerts.length === 0 ? "loading" : "ready";
    });
    try {
      const res = await this.admin.getAlerts({
        lastN: this.lastN,
        since: this.cursor || undefined,
        signal: ctl.signal,
      });
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.mergeAlerts(res.alerts);
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

  private mergeAlerts(incoming: P2pAlertEventDto[]) {
    if (incoming.length === 0) return;
    const seen = new Set(this.alerts.map((a) => a.id));
    const fresh = incoming.filter((a) => !seen.has(a.id));
    if (fresh.length === 0) return;
    // Backend returns newest-first; prepend.
    this.alerts = [...fresh, ...this.alerts];
    this.newSinceLastTick = [...fresh, ...this.newSinceLastTick];
    const maxTs = Math.max(this.cursor, ...fresh.map((a) => a.alertUnixMs));
    this.cursor = maxTs;
  }
}
