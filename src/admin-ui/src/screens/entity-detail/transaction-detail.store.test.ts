import { describe, expect, it, vi } from "vitest";
import { TransactionDetailStore } from "./transaction-detail.store";
import { EventBus } from "@/lib/events/bus";

const TXID = "aabbccdd11223344556677889900aabbccdd11223344556677889900aabbccdd";
const OTHER_TXID = "ffeeddccbbaa00998877665544332211ffeeddccbbaa00998877665544332211";

function build(opts: { subscribe?: ReturnType<typeof vi.fn> } = {}) {
  const bus = new EventBus();
  const subscribe =
    opts.subscribe ?? vi.fn().mockResolvedValue(undefined);
  const signalR = { subscribeToBroadcast: subscribe };
  const store = new TransactionDetailStore({ txId: TXID, bus, signalR });
  return { store, bus, signalR, subscribe };
}

function emit(bus: EventBus, txId: string, state: string, t = Date.now(), failReason: string | null = null) {
  bus.emit("OnBroadcastStateChanged", { txId, state, updatedAtMs: t, failReason });
}

describe("TransactionDetailStore", () => {
  it("subscribes to the hub on start", async () => {
    const { store, subscribe } = build();
    await store.start();
    expect(subscribe).toHaveBeenCalledWith(TXID);
    expect(store.subscribed).toBe(true);
    expect(store.subscribeError).toBeNull();
    store.dispose();
  });

  it("captures subscribe errors without blocking the bus", async () => {
    const subscribe = vi.fn().mockRejectedValue(new Error("hub disconnected"));
    const { store, bus } = build({ subscribe });
    await store.start();
    expect(store.subscribed).toBe(false);
    expect(store.subscribeError).toBe("hub disconnected");
    emit(bus, TXID, "Validated");
    expect(store.history).toHaveLength(1);
    store.dispose();
  });

  it("only accepts events for its txId", async () => {
    const { store, bus } = build();
    await store.start();
    emit(bus, OTHER_TXID, "Validated");
    emit(bus, TXID, "Validated");
    expect(store.history).toHaveLength(1);
    expect(store.history[0].state).toBe("Validated");
    store.dispose();
  });

  it("dedupes identical events", async () => {
    const { store, bus } = build();
    await store.start();
    emit(bus, TXID, "Validated", 1_000);
    emit(bus, TXID, "Validated", 1_000);
    expect(store.history).toHaveLength(1);
    store.dispose();
  });

  it("advances the happy-path stages as events arrive", async () => {
    const { store, bus } = build();
    await store.start();
    emit(bus, TXID, "Validated", 1_000);
    let stages = store.stagesView;
    expect(stages[0].status).toBe("active");
    expect(stages[1].status).toBe("pending");

    emit(bus, TXID, "Dispatching", 2_000);
    stages = store.stagesView;
    expect(stages[0].status).toBe("done");
    expect(stages[1].status).toBe("active");

    emit(bus, TXID, "PeerRelayed", 3_000);
    stages = store.stagesView;
    expect(stages[1].status).toBe("done");
    expect(stages[2].status).toBe("active");

    emit(bus, TXID, "Mined", 4_000);
    stages = store.stagesView;
    expect(stages[3].status).toBe("active");

    emit(bus, TXID, "Confirmed", 5_000);
    stages = store.stagesView;
    expect(stages[4].status).toBe("done");
    expect(store.isTerminal).toBe(true);
    store.dispose();
  });

  it("marks the owning stage as failed on a failure state", async () => {
    const { store, bus } = build();
    await store.start();
    emit(bus, TXID, "Validated", 1_000);
    emit(bus, TXID, "Dispatching", 2_000);
    emit(bus, TXID, "ConflictRejected", 3_000, "double_spend");
    const stages = store.stagesView;
    expect(stages[1].status).toBe("failed");
    // ConflictRejected is one of TX_TERMINAL_STATES — no further
    // updates expected; UI should stop polling for transitions.
    expect(store.isTerminal).toBe(true);
    store.dispose();
  });

  it("unwires bus subscription on dispose()", async () => {
    const { store, bus } = build();
    await store.start();
    store.dispose();
    emit(bus, TXID, "Validated");
    expect(store.history).toHaveLength(0);
  });

  it("start() is idempotent", async () => {
    const { store, bus, subscribe } = build();
    await store.start();
    await store.start();
    expect(subscribe).toHaveBeenCalledTimes(1);
    emit(bus, TXID, "Validated");
    // Only one subscription registered → only one event recorded.
    expect(store.history).toHaveLength(1);
    store.dispose();
  });
});
