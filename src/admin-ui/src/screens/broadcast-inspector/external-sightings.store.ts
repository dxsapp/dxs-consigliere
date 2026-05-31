import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * Broadcast inspector — independent third-party confirmation that a tx the
 * thin node broadcast over its OWN P2P pool actually reached the public
 * indexers. After a broadcast, one poller per source asks the backend
 * (GET /api/tx/{txId}/external-sighting/{source}) every second; the moment a
 * source reports `seen`, that indicator flips to OK and its poller stops.
 *
 * Pollers stop independently (seen, or after `maxAttempts` give-up). Timers
 * are injectable so the orchestration is unit-testable without real intervals.
 */

export type SightingSource = "woc" | "bitails" | "junglebus";

export interface SightingState {
  source: SightingSource;
  /** Human label for the row. */
  label: string;
  /** The explorer has seen the txid. */
  seen: boolean;
  /** A poller is currently running for this source. */
  polling: boolean;
}

const SOURCES: ReadonlyArray<{ source: SightingSource; label: string }> = [
  { source: "woc", label: "WhatsOnChain" },
  { source: "bitails", label: "Bitails" },
  { source: "junglebus", label: "JungleBus" },
];

const DEFAULT_POLL_MS = 1_000;
const DEFAULT_MAX_ATTEMPTS = 120; // ~2 min at 1s before giving up

export interface ExternalSightingsStoreOptions {
  admin: Pick<IAdminClient, "getExternalSighting">;
  txId: string;
  pollMs?: number;
  maxAttempts?: number;
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

export class ExternalSightingsStore {
  sightings: SightingState[] = SOURCES.map((s) => ({
    source: s.source,
    label: s.label,
    seen: false,
    polling: false,
  }));

  private readonly admin: Pick<IAdminClient, "getExternalSighting">;
  private readonly txId: string;
  private readonly pollMs: number;
  private readonly maxAttempts: number;
  private readonly setIntervalFn: NonNullable<ExternalSightingsStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<ExternalSightingsStoreOptions["clearInterval"]>;
  private readonly timers = new Map<SightingSource, ReturnType<typeof setInterval>>();
  private readonly attempts = new Map<SightingSource, number>();

  constructor(opts: ExternalSightingsStoreOptions) {
    this.admin = opts.admin;
    this.txId = opts.txId;
    this.pollMs = opts.pollMs ?? DEFAULT_POLL_MS;
    this.maxAttempts = opts.maxAttempts ?? DEFAULT_MAX_ATTEMPTS;
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /** Begin polling every source. Safe to call once on mount. */
  start(): void {
    if (!this.txId) return;
    for (const s of SOURCES) this.startSource(s.source);
  }

  dispose(): void {
    for (const h of this.timers.values()) this.clearIntervalFn(h);
    this.timers.clear();
  }

  private startSource(source: SightingSource): void {
    if (this.timers.has(source)) return;
    this.attempts.set(source, 0);
    this.setPolling(source, true);
    void this.poll(source); // immediate first probe
    const h = this.setIntervalFn(() => void this.poll(source), this.pollMs);
    this.timers.set(source, h);
  }

  private async poll(source: SightingSource): Promise<void> {
    const attempt = (this.attempts.get(source) ?? 0) + 1;
    this.attempts.set(source, attempt);
    try {
      const res = await this.admin.getExternalSighting(this.txId, source);
      if (res.seen) {
        this.markSeen(source);
        this.stopSource(source);
        return;
      }
    } catch {
      /* transient — keep polling until maxAttempts */
    }
    if (attempt >= this.maxAttempts) this.stopSource(source);
  }

  private stopSource(source: SightingSource): void {
    const h = this.timers.get(source);
    if (h !== undefined) this.clearIntervalFn(h);
    this.timers.delete(source);
    this.setPolling(source, false);
  }

  private setPolling(source: SightingSource, polling: boolean): void {
    runInAction(() => {
      const s = this.sightings.find((x) => x.source === source);
      if (s) s.polling = polling;
    });
  }

  private markSeen(source: SightingSource): void {
    runInAction(() => {
      const s = this.sightings.find((x) => x.source === source);
      if (s) {
        s.seen = true;
        s.polling = false;
      }
    });
  }
}
