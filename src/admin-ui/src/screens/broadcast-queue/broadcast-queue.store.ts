import { makeAutoObservable, runInAction } from "mobx";
import type { EventBus, EventMap, Unsubscribe } from "@/lib/events/bus";
import {
  isTxStateFailure,
  isTxStateTerminal,
  type OutgoingTxState,
} from "@/types/admin";

/**
 * S6 — Broadcast Queue store. Live kanban backed by the event bus.
 *
 * Lanes (operator semantics):
 *   - Validated     — accepted but not yet on the wire.
 *   - Dispatching   — sending INV / awaiting peer-ack (incl. PeerAcked).
 *   - PeerRelayed   — at least one peer relayed; visible in mempool.
 *
 * Terminal states (Confirmed / Mined / failures) drop off the kanban
 * after a configurable lingering window so the operator can confirm
 * the transition before the card vanishes.
 *
 * Cards stuck in Dispatching beyond `staleAfterMs` (default 5 min)
 * are flagged `stale` — the UI tints them.
 *
 * Per Core Rule §6 every wire is unwound in `dispose()`.
 */
export type BroadcastLane = "validated" | "dispatching" | "peerRelayed";

export interface QueueCard {
  txId: string;
  state: OutgoingTxState;
  lane: BroadcastLane;
  updatedAtMs: number;
  enteredLaneAtMs: number;
  failReason: string | null;
  /** True when the latest state is a terminal/failure type. The card
   *  is still rendered until `terminalLingerMs` expires so the
   *  operator can see the transition. */
  terminal: boolean;
}

export type QueueEvent = EventMap["OnBroadcastStateChanged"];

const LANE_BY_STATE: Record<OutgoingTxState, BroadcastLane | null> = {
  Submitted: null,
  Validated: "validated",
  Dispatching: "dispatching",
  PeerAcked: "dispatching",
  PeerRelayed: "peerRelayed",
  MempoolSeen: "peerRelayed",
  // Terminal — keep card on its prior lane (set when first seen).
  Mined: null,
  Confirmed: null,
  PolicyInvalid: null,
  InvalidRejected: null,
  ConflictRejected: null,
  EvictedOrDropped: null,
  ObserverUnknown: null,
  Failed: null,
};

export interface BroadcastQueueStoreOptions {
  bus: EventBus;
  /** Stale threshold for Dispatching cards. Default 5 min. */
  staleAfterMs?: number;
  /** Linger for terminal cards before dropping off. Default 30s. */
  terminalLingerMs?: number;
  /** Test seam — pluggable clock. */
  now?: () => number;
}

const DEFAULT_STALE_AFTER_MS = 5 * 60 * 1000;
const DEFAULT_TERMINAL_LINGER_MS = 30_000;

export class BroadcastQueueStore {
  /** Newest-first ordering inside each lane. */
  private cards = new Map<string, QueueCard>();
  /** Tick counter so MobX recomputes the lane getters when only
   *  staleness changes (no card add/move). Bumped by markStale(). */
  private staleTick = 0;

  private readonly bus: EventBus;
  private readonly staleAfterMs: number;
  private readonly terminalLingerMs: number;
  private readonly now: () => number;
  private disposers: Unsubscribe[] = [];
  private sweepTimer: ReturnType<typeof setInterval> | null = null;

  constructor(opts: BroadcastQueueStoreOptions) {
    this.bus = opts.bus;
    this.staleAfterMs = opts.staleAfterMs ?? DEFAULT_STALE_AFTER_MS;
    this.terminalLingerMs = opts.terminalLingerMs ?? DEFAULT_TERMINAL_LINGER_MS;
    this.now = opts.now ?? (() => Date.now());
    makeAutoObservable(this, {}, { autoBind: true });
  }

  start(): void {
    if (this.disposers.length > 0) return;
    this.disposers.push(
      this.bus.on("OnBroadcastStateChanged", (evt) => this.recordEvent(evt))
    );
    // Tick once every 15s — sweeps expired terminals + bumps the
    // stale-tick so the UI rerenders the stale flag.
    this.sweepTimer = setInterval(() => this.sweep(), 15_000);
  }

  dispose(): void {
    for (const off of this.disposers) off();
    this.disposers = [];
    if (this.sweepTimer) {
      clearInterval(this.sweepTimer);
      this.sweepTimer = null;
    }
  }

  /** Test-only mutation: lets a fake-timer test step the sweep
   *  without waiting for the interval. */
  forceSweep(): void {
    this.sweep();
  }

  get validated(): QueueCard[] {
    return this.byLane("validated");
  }
  get dispatching(): QueueCard[] {
    return this.byLane("dispatching");
  }
  get peerRelayed(): QueueCard[] {
    return this.byLane("peerRelayed");
  }

  /** Convenience for the page header counter. */
  get totalActive(): number {
    return this.validated.length + this.dispatching.length + this.peerRelayed.length;
  }

  /** True iff the card is in Dispatching past `staleAfterMs`. */
  isStale(card: QueueCard): boolean {
    // Touch the tick so MobX rerenders derived UI when the sweep
    // bumps it. (The tick is observable; the getter is in an
    // observer-wrapped component.)
    void this.staleTick;
    if (card.lane !== "dispatching") return false;
    return this.now() - card.enteredLaneAtMs > this.staleAfterMs;
  }

  // ── Internal ─────────────────────────────────────────────────

  private byLane(lane: BroadcastLane): QueueCard[] {
    void this.staleTick;
    const list = Array.from(this.cards.values()).filter((c) => c.lane === lane);
    list.sort((a, b) => b.updatedAtMs - a.updatedAtMs);
    return list;
  }

  private recordEvent(evt: QueueEvent) {
    const state = evt.state as OutgoingTxState;
    const targetLane = LANE_BY_STATE[state];
    const terminal = isTxStateTerminal(state) || isTxStateFailure(state);
    const existing = this.cards.get(evt.txId);

    runInAction(() => {
      // Pre-lane state (Submitted) — only record if we already have
      // a card; otherwise wait for the first lane-bearing state.
      if (targetLane === null && !terminal) {
        if (!existing) return;
        existing.state = state;
        existing.updatedAtMs = evt.updatedAtMs;
        existing.failReason = evt.failReason;
        return;
      }
      if (terminal) {
        if (!existing) return; // never had a card; drop the terminal.
        existing.state = state;
        existing.updatedAtMs = evt.updatedAtMs;
        existing.failReason = evt.failReason;
        existing.terminal = true;
        return;
      }
      // Lane-bearing state.
      if (!existing) {
        this.cards.set(evt.txId, {
          txId: evt.txId,
          state,
          lane: targetLane!,
          updatedAtMs: evt.updatedAtMs,
          enteredLaneAtMs: this.now(),
          failReason: evt.failReason,
          terminal: false,
        });
        return;
      }
      const laneChanged = existing.lane !== targetLane!;
      existing.state = state;
      existing.lane = targetLane!;
      existing.updatedAtMs = evt.updatedAtMs;
      existing.failReason = evt.failReason;
      existing.terminal = false;
      if (laneChanged) existing.enteredLaneAtMs = this.now();
    });
  }

  private sweep() {
    const cutoff = this.now() - this.terminalLingerMs;
    runInAction(() => {
      let removed = false;
      for (const [id, c] of this.cards) {
        if (c.terminal && c.updatedAtMs < cutoff) {
          this.cards.delete(id);
          removed = true;
        }
      }
      // Tick bump — drives re-eval of `isStale` even when no card
      // was added/moved/removed.
      this.staleTick = (this.staleTick + 1) % 1_000_000;
      // Touch a no-op to ensure MobX flags an observable change if
      // we only removed cards. (cards is a plain Map field; deletes
      // are tracked because makeAutoObservable wraps it.)
      void removed;
    });
  }
}
