/**
 * Typed publish/subscribe event bus (Core Rule §4 / A1 M7).
 *
 * The SignalR client publishes hub events here; screen stores
 * subscribe to the events they care about and unsubscribe in
 * `dispose()`. Decouples the network layer from MobX stores
 * without spreading SignalR-specific imports through screens.
 *
 * Subscribe returns an unsubscribe function. Callers MUST call it
 * on store/component unmount (Core Rule §6 — background cleanup).
 */

/**
 * Typed events the bus carries. Add new entries here as new hub
 * methods come online; the rest of the codebase reaches typesafe
 * subscribers via `EventMap[K]`.
 *
 * Names match the backend `IWalletHub` methods so an audit can
 * grep both sides.
 */
export interface EventMap {
  /** Wave 1 hub event. New chain tip; carries the full BlockTipDto. */
  OnNewBlock: {
    hash: string;
    height: number;
    timestampMs: number;
    prevHash: string;
    headerSize: number;
  };

  /** Wave 3 hub event. Reorg with degraded-state hint. */
  OnReorg: {
    commonAncestorHash: string;
    commonAncestorHeight: number;
    orphanedHashes: string[];
    newTipHash: string;
    newTipHeight: number;
    degradedState: boolean;
  };

  /** W2 + W5 hub event. Tx lifecycle transition.
   *  `failReason` is `string | null` (not optional) because
   *  SignalR's default JSON protocol serializes the C#
   *  `string FailReason = null` field as a literal null on the
   *  wire — S3-audit M4. */
  OnBroadcastStateChanged: {
    txId: string;
    state: string;
    updatedAtMs: number;
    failReason: string | null;
  };

  /** SignalR connection-state lifecycle. Synthesised by the
   *  SignalR client; not an actual hub method. The shell store
   *  subscribes to this to update its `connection` field. */
  ConnectionStateChanged: {
    status: "online" | "offline" | "stale";
  };
}

export type EventKey = keyof EventMap;

export type Handler<K extends EventKey> = (payload: EventMap[K]) => void;

export type Unsubscribe = () => void;

export class EventBus {
  private readonly handlers = new Map<EventKey, Set<Handler<EventKey>>>();

  /** Subscribe a handler. Returns an unsubscribe callback. */
  on<K extends EventKey>(key: K, handler: Handler<K>): Unsubscribe {
    let set = this.handlers.get(key);
    if (!set) {
      set = new Set();
      this.handlers.set(key, set);
    }
    set.add(handler as Handler<EventKey>);
    return () => {
      const s = this.handlers.get(key);
      if (!s) return;
      s.delete(handler as Handler<EventKey>);
      if (s.size === 0) this.handlers.delete(key);
    };
  }

  /** Publish to all subscribers. Errors in one handler do not
   *  block delivery to the rest. */
  emit<K extends EventKey>(key: K, payload: EventMap[K]): void {
    const set = this.handlers.get(key);
    if (!set) return;
    for (const handler of [...set]) {
      try {
        (handler as Handler<K>)(payload);
      } catch (err) {
        // Per Core Rule §9 we don't log full payloads; surface the
        // error category only. (console.warn is allowed by the
        // ESLint policy in eslint.config.js.)
        console.warn(`[EventBus] handler for ${key} threw`, err);
      }
    }
  }

  /** Test-only: count active subscribers per key. */
  listenerCount(key: EventKey): number {
    return this.handlers.get(key)?.size ?? 0;
  }

  /** Test-only: wipe everything. */
  reset(): void {
    this.handlers.clear();
  }
}
