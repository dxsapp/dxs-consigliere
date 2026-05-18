import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BroadcastQueueStore } from "./broadcast-queue.store";
import { EventBus } from "@/lib/events/bus";

const TX1 = "a".repeat(64);
const TX2 = "b".repeat(64);

function build(opts: { now?: () => number; staleAfterMs?: number; terminalLingerMs?: number } = {}) {
  const bus = new EventBus();
  const store = new BroadcastQueueStore({
    bus,
    now: opts.now,
    staleAfterMs: opts.staleAfterMs,
    terminalLingerMs: opts.terminalLingerMs,
  });
  return { bus, store };
}

function emit(bus: EventBus, txId: string, state: string, t = 1_000, failReason: string | null = null) {
  bus.emit("OnBroadcastStateChanged", { txId, state, updatedAtMs: t, failReason });
}

describe("BroadcastQueueStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("places a Validated event into the Validated lane", () => {
    const { bus, store } = build();
    store.start();
    emit(bus, TX1, "Validated", 1_000);
    expect(store.validated).toHaveLength(1);
    expect(store.dispatching).toHaveLength(0);
    expect(store.peerRelayed).toHaveLength(0);
    expect(store.totalActive).toBe(1);
    store.dispose();
  });

  it("moves a card across lanes on subsequent events", () => {
    const { bus, store } = build();
    store.start();
    emit(bus, TX1, "Validated", 1_000);
    emit(bus, TX1, "Dispatching", 2_000);
    expect(store.validated).toHaveLength(0);
    expect(store.dispatching).toHaveLength(1);
    emit(bus, TX1, "PeerRelayed", 3_000);
    expect(store.dispatching).toHaveLength(0);
    expect(store.peerRelayed).toHaveLength(1);
    store.dispose();
  });

  it("collapses PeerAcked into the Dispatching lane", () => {
    const { bus, store } = build();
    store.start();
    emit(bus, TX1, "PeerAcked", 1_000);
    expect(store.dispatching).toHaveLength(1);
    store.dispose();
  });

  it("collapses MempoolSeen into the PeerRelayed lane", () => {
    const { bus, store } = build();
    store.start();
    emit(bus, TX1, "MempoolSeen", 1_000);
    expect(store.peerRelayed).toHaveLength(1);
    store.dispose();
  });

  it("flags Dispatching cards stale after staleAfterMs", () => {
    let clock = 1_000;
    const { bus, store } = build({ now: () => clock, staleAfterMs: 60_000 });
    store.start();
    emit(bus, TX1, "Dispatching", clock);
    const card = store.dispatching[0];
    expect(store.isStale(card)).toBe(false);
    clock += 30_000;
    expect(store.isStale(card)).toBe(false);
    clock += 60_000;
    expect(store.isStale(card)).toBe(true);
    store.dispose();
  });

  it("retains a terminal card until linger expires then drops it", () => {
    let clock = 1_000;
    const { bus, store } = build({ now: () => clock, terminalLingerMs: 1_000 });
    store.start();
    emit(bus, TX1, "Dispatching", clock);
    expect(store.dispatching).toHaveLength(1);
    clock += 500;
    emit(bus, TX1, "Confirmed", clock);
    // Terminal events keep the card on its prior lane until sweep.
    const stillThere = store.dispatching.find((c) => c.txId === TX1);
    expect(stillThere?.terminal).toBe(true);
    clock += 5_000;
    store.forceSweep();
    expect(store.totalActive).toBe(0);
    store.dispose();
  });

  it("ignores a terminal event for an unknown txid", () => {
    const { bus, store } = build();
    store.start();
    emit(bus, TX1, "Confirmed", 1_000);
    expect(store.totalActive).toBe(0);
    store.dispose();
  });

  it("dispose() unwires the bus subscription", () => {
    const { bus, store } = build();
    store.start();
    store.dispose();
    emit(bus, TX1, "Validated");
    expect(store.totalActive).toBe(0);
  });

  it("two tx ids occupy distinct slots in the same lane", () => {
    const { bus, store } = build();
    store.start();
    emit(bus, TX1, "Validated", 1_000);
    emit(bus, TX2, "Validated", 2_000);
    expect(store.validated).toHaveLength(2);
    // Newest first.
    expect(store.validated[0].txId).toBe(TX2);
    store.dispose();
  });
});
