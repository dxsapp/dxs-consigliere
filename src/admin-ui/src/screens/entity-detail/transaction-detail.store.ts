import { makeAutoObservable, runInAction } from "mobx";
import type { EntityTimelineStage } from "@/screens/entity-detail/EntityTimeline";
import type { EventBus, EventMap, Unsubscribe } from "@/lib/events/bus";
import type { ISignalRClient } from "@/lib/signalr/client";
import {
  isTxStateFailure,
  isTxStateTerminal,
  TX_HAPPY_PATH,
  type OutgoingTxState,
} from "@/types/admin";

/**
 * S5 — Transaction detail store. Listens for `OnBroadcastStateChanged`
 * for a specific `txId`, accumulates the state history, and projects
 * it onto the 5-stage happy-path Stepper.
 *
 * Per Core Rule §6 every async wire is unwound by `dispose()`:
 *  - bus subscription
 *  - signalR.subscribeToBroadcast invoke is fire-and-forget (the
 *    hub doesn't expose unsubscribe yet; the bus filter is the
 *    primary mechanism that scopes events to the active screen)
 *
 * The 14-state backend enum collapses to a 5-stage operator view:
 *
 *   Validated | Dispatching | PeerRelayed | Mined | Confirmed
 *
 * Backend states that don't add visual signal collapse into the
 * stage they share a phase with:
 *  - Submitted → not advanced (pre-Validated)
 *  - PeerAcked → still "Dispatching" (peer accepted INV but no relay
 *    confirmation yet)
 *  - MempoolSeen → equivalent to PeerRelayed in operator semantics
 *  - Failure states mark the current stage as failed.
 */

export type TxStateEvent = EventMap["OnBroadcastStateChanged"];

const STAGE_BY_STATE: Record<OutgoingTxState, number | null> = {
  // Pre-stage — not yet on the Stepper.
  Submitted: null,
  ObserverUnknown: null,
  // Stage 0 — validated.
  Validated: 0,
  // Stage 1 — dispatching (incl. peer-ack mid-state).
  Dispatching: 1,
  PeerAcked: 1,
  // Stage 2 — relayed / visible in mempool.
  PeerRelayed: 2,
  MempoolSeen: 2,
  // Stage 3 — confirmed in a block but not yet stable.
  Mined: 3,
  // Stage 4 — confirmed past finality threshold.
  Confirmed: 4,
  // Failure states pin to the stage that owns them (best-effort).
  PolicyInvalid: 0,
  InvalidRejected: 1,
  ConflictRejected: 1,
  EvictedOrDropped: 2,
  Failed: 1,
};

const STAGE_LABELS = ["Validated", "Dispatching", "Peer-relayed", "Mined", "Confirmed"] as const;

export interface TransactionDetailStoreOptions {
  txId: string;
  bus: EventBus;
  /** Optional: server-side subscription. Mocks may omit this; the
   *  bus delivers events regardless of subscribe outcome. */
  signalR?: Pick<ISignalRClient, "subscribeToBroadcast">;
}

export class TransactionDetailStore {
  readonly txId: string;

  /** Newest-first history of received state events. */
  history: TxStateEvent[] = [];

  /** True after subscribe() resolves (success or failure — failure
   *  surfaces in `subscribeError`, not by blocking events). */
  subscribed = false;
  subscribeError: string | null = null;

  private readonly bus: EventBus;
  private readonly signalR?: Pick<ISignalRClient, "subscribeToBroadcast">;
  private disposers: Unsubscribe[] = [];

  constructor(opts: TransactionDetailStoreOptions) {
    this.txId = opts.txId;
    this.bus = opts.bus;
    this.signalR = opts.signalR;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /** Wire up. Idempotent under StrictMode. */
  async start(): Promise<void> {
    if (this.disposers.length > 0) return;
    this.disposers.push(
      this.bus.on("OnBroadcastStateChanged", (evt) => {
        if (evt.txId !== this.txId) return;
        this.appendEvent(evt);
      })
    );
    if (this.signalR) {
      try {
        await this.signalR.subscribeToBroadcast(this.txId);
        runInAction(() => {
          this.subscribed = true;
          this.subscribeError = null;
        });
      } catch (err) {
        runInAction(() => {
          this.subscribed = false;
          this.subscribeError = errorMessage(err);
        });
      }
    }
  }

  dispose(): void {
    for (const off of this.disposers) off();
    this.disposers = [];
  }

  /** Newest event seen. Drives the timeline status. */
  get latest(): TxStateEvent | null {
    return this.history[0] ?? null;
  }

  /** Whether the latest state is terminal — keeps the UI from
   *  flagging "in flight" once we know we're done. */
  get isTerminal(): boolean {
    const last = this.latest;
    return last !== null && isTxStateTerminal(last.state as OutgoingTxState);
  }

  /** Backend states grouped by stage (0..4) for the detail slot
   *  underneath each step in the timeline. */
  get stagesView(): EntityTimelineStage[] {
    const last = this.latest;
    const lastState = last ? (last.state as OutgoingTxState) : null;
    const failed = lastState ? isTxStateFailure(lastState) : false;
    const currentStage = lastState ? STAGE_BY_STATE[lastState] : null;

    return TX_HAPPY_PATH.map((canonical, idx) => {
      // Aggregate every observed event mapping to this stage, newest
      // first; that's what the operator wants to read.
      const events = this.history.filter(
        (e) => STAGE_BY_STATE[e.state as OutgoingTxState] === idx
      );
      const newest = events[0] ?? null;

      let status: EntityTimelineStage["status"];
      if (failed && currentStage === idx) {
        status = "failed";
      } else if (currentStage !== null && idx < currentStage) {
        status = "done";
      } else if (currentStage === idx) {
        status = events.length > 0 ? (idx === 4 ? "done" : "active") : "active";
      } else if (currentStage !== null && idx > currentStage) {
        status = "pending";
      } else {
        status = "pending";
      }

      return {
        key: canonical,
        label: STAGE_LABELS[idx],
        status,
        timestampMs: newest?.updatedAtMs ?? null,
        detail: newest ? this.renderDetail(events) : null,
      };
    });
  }

  // ── Internal ─────────────────────────────────────────────────

  private renderDetail(events: TxStateEvent[]): string {
    // Newest first; show the most recent state plus the optional
    // failReason. The DOM renders as plain text — sufficient to
    // surface backend signal without re-implementing typography.
    const newest = events[0];
    const fail = newest.failReason ? ` · ${newest.failReason}` : "";
    return `${newest.state}${fail}`;
  }

  private appendEvent(evt: TxStateEvent) {
    runInAction(() => {
      // Dedupe by (state, updatedAtMs) so a duplicate hub emit
      // doesn't double-stamp the timeline.
      const dup = this.history.find(
        (e) => e.state === evt.state && e.updatedAtMs === evt.updatedAtMs
      );
      if (dup) return;
      this.history = [evt, ...this.history];
    });
  }
}

function errorMessage(err: unknown): string {
  if (err && typeof err === "object" && "message" in err && typeof err.message === "string") {
    return err.message;
  }
  return "Unknown error";
}
