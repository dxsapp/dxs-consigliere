import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  AdminPeerRow,
  AdminPeersResponse,
  P2pHealthDto,
} from "@/types/admin";

/**
 * S8 — P2P Pool store. Combines health (poolSize / target / /24
 * diversity) + per-peer roster + the per-peer ScoreBar inputs.
 *
 * Score derivation (operator-correct, not authoritative):
 *   accept    = successCount / (successCount + failCount)   range [0,1]
 *   recency   = 1 - clamp((now - lastSeen) / 24h, 0, 1)     range [0,1]
 *   diversity = 1 if peer's /24 is otherwise unrepresented  binary
 *   composite = 0.5*accept + 0.3*recency + 0.2*diversity
 *
 * The per-peer mini-bars expose each component so the operator
 * can see WHY a peer scored low (instead of guessing).
 */
export interface PeerScore {
  /** [0,1] — fraction of attempts that succeeded. */
  accept: number;
  /** [0,1] — 1.0 == seen recently, decays linearly over 24h. */
  recency: number;
  /** 0 or 1 — bonus when the peer brings a new /24 subnet. */
  diversity: number;
  /** 0..100 composite score. */
  composite: number;
}

export interface ScoredPeer extends AdminPeerRow {
  score: PeerScore;
}

export interface P2pStoreOptions {
  admin: IAdminClient;
  pollMs?: number;
  now?: () => number;
  setInterval?: (cb: () => void, ms: number) => ReturnType<typeof setInterval>;
  clearInterval?: (h: ReturnType<typeof setInterval>) => void;
}

const DEFAULT_POLL_MS = 30_000;

export class P2pStore {
  health: P2pHealthDto | null = null;
  peers: AdminPeersResponse | null = null;
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;

  private readonly admin: IAdminClient;
  private readonly pollMs: number;
  private readonly nowFn: () => number;
  private readonly setIntervalFn: NonNullable<P2pStoreOptions["setInterval"]>;
  private readonly clearIntervalFn: NonNullable<P2pStoreOptions["clearInterval"]>;
  private timer: ReturnType<typeof setInterval> | null = null;
  private inflight: AbortController | null = null;

  constructor(opts: P2pStoreOptions) {
    this.admin = opts.admin;
    this.pollMs = opts.pollMs ?? DEFAULT_POLL_MS;
    this.nowFn = opts.now ?? (() => Date.now());
    this.setIntervalFn = opts.setInterval ?? ((cb, ms) => setInterval(cb, ms));
    this.clearIntervalFn = opts.clearInterval ?? ((h) => clearInterval(h));
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
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

  async refresh(): Promise<void> {
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.health ? "ready" : "loading";
    });
    try {
      const [health, peers] = await Promise.all([
        this.admin.getP2pHealth(ctl.signal),
        this.admin.getPeers(ctl.signal),
      ]);
      if (ctl.signal.aborted ) return;
      runInAction(() => {
        this.health = health;
        this.peers = peers;
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

  /** Scored, sorted-by-composite-desc peers. */
  get scoredPeers(): ScoredPeer[] {
    if (!this.peers) return [];
    const all = this.peers.peers;
    const subnetCounts = new Map<string, number>();
    for (const p of all) {
      subnetCounts.set(p.subnet24, (subnetCounts.get(p.subnet24) ?? 0) + 1);
    }
    const now = this.nowFn();
    const scored = all.map((p) => {
      const total = p.successCount + p.failCount;
      const accept = total === 0 ? 0 : p.successCount / total;
      const recency = recencyFromLastSeen(p.lastSeen, now);
      const diversity = (subnetCounts.get(p.subnet24) ?? 0) === 1 ? 1 : 0;
      const composite = Math.round(
        (0.5 * accept + 0.3 * recency + 0.2 * diversity) * 100
      );
      return {
        ...p,
        score: { accept, recency, diversity, composite },
      };
    });
    scored.sort((a, b) => b.score.composite - a.score.composite);
    return scored;
  }

  /** [{ subnet, count }] for the /24 donut, largest first. */
  get subnetBreakdown(): Array<{ subnet: string; count: number }> {
    if (!this.peers) return [];
    const counts = new Map<string, number>();
    for (const p of this.peers.peers) {
      counts.set(p.subnet24, (counts.get(p.subnet24) ?? 0) + 1);
    }
    return Array.from(counts.entries())
      .map(([subnet, count]) => ({ subnet, count }))
      .sort((a, b) => b.count - a.count);
  }
}

function recencyFromLastSeen(lastSeenIso: string | null, nowMs: number): number {
  if (!lastSeenIso) return 0;
  const t = Date.parse(lastSeenIso);
  if (!Number.isFinite(t)) return 0;
  const ageHours = (nowMs - t) / (60 * 60 * 1000);
  if (ageHours <= 0) return 1;
  if (ageHours >= 24) return 0;
  return 1 - ageHours / 24;
}
