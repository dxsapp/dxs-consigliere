import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { EventBus, Unsubscribe } from "@/lib/events/bus";
import type { HeadersTipDto } from "@/types/admin";

/**
 * S9 — Headers chain store. Polls the headers tip + recent list and
 * also subscribes to bus.OnNewBlock so the tip updates live without
 * waiting for the next poll. OnReorg surfaces as `reorgEvents`.
 */
export interface HeadersStoreOptions {
  admin: IAdminClient;
  bus: EventBus;
  pollMs?: number;
  recentCount?: number;
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

const DEFAULT_POLL_MS = 30_000;
const DEFAULT_RECENT = 20;

export interface ReorgEntry {
  commonAncestorHeight: number;
  orphanedHashes: string[];
  newTipHash: string;
  newTipHeight: number;
  degradedState: boolean;
  observedAtMs: number;
}

export class HeadersStore {
  tip: HeadersTipDto | null = null;
  recent: HeadersTipDto[] = [];
  reorgEvents: ReorgEntry[] = [];
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;

  private readonly admin: IAdminClient;
  private readonly bus: EventBus;
  private readonly pollMs: number;
  private readonly recentCount: number;
  private readonly setIntervalFn: NonNullable<HeadersStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<HeadersStoreOptions["clearInterval"]>;
  private timer: ReturnType<typeof setInterval> | null = null;
  private inflight: AbortController | null = null;
  private disposers: Unsubscribe[] = [];
  private disposed = false;

  constructor(opts: HeadersStoreOptions) {
    this.admin = opts.admin;
    this.bus = opts.bus;
    this.pollMs = opts.pollMs ?? DEFAULT_POLL_MS;
    this.recentCount = opts.recentCount ?? DEFAULT_RECENT;
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    if (this.disposed || this.timer) return;
    this.disposers.push(
      this.bus.on("OnReorg", (evt) =>
        runInAction(() => {
          this.reorgEvents = [
            {
              commonAncestorHeight: evt.commonAncestorHeight,
              orphanedHashes: evt.orphanedHashes,
              newTipHash: evt.newTipHash,
              newTipHeight: evt.newTipHeight,
              degradedState: evt.degradedState,
              observedAtMs: Date.now(),
            },
            ...this.reorgEvents,
          ].slice(0, 50);
        })
      )
    );
    await this.refresh();
    this.timer = this.setIntervalFn(() => void this.refresh(), this.pollMs);
  }

  dispose(): void {
    this.disposed = true;
    if (this.timer) this.clearIntervalFn(this.timer);
    this.timer = null;
    this.inflight?.abort();
    this.inflight = null;
    for (const off of this.disposers) off();
    this.disposers = [];
  }

  async refresh(): Promise<void> {
    if (this.disposed) return;
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.tip ? "ready" : "loading";
    });
    try {
      const [tip, recent] = await Promise.all([
        this.admin.getHeadersTip(ctl.signal),
        this.admin.getHeadersRecent(this.recentCount, ctl.signal),
      ]);
      if (ctl.signal.aborted || this.disposed) return;
      runInAction(() => {
        this.tip = tip;
        this.recent = recent;
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
}
