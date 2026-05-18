import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EventBus } from "@/lib/events/bus";
import { MockSignalRClient } from "@/lib/mock/signalr";

/**
 * S3 cleanup harness (A1 M2 / Core Rule §6).
 *
 * Pins the contract that every background subscriber / timer /
 * SignalR client tears down deterministically on unmount.
 * Integration-level: drives the EventBus + MockSignalRClient pair
 * end to end with fake timers, no React tree.
 *
 * Real-world screen stores will register bus.on(...) in their
 * constructors and call the returned `Unsubscribe` in `dispose()`.
 * These tests prove the primitives the screens rely on are
 * leak-free.
 */
describe("cleanup harness — bus subscribers + SignalR timers", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("unsubscribed handlers do not receive subsequent emits", () => {
    const bus = new EventBus();
    const seen: string[] = [];
    const off = bus.on("OnBroadcastStateChanged", (e) => seen.push(e.state));

    bus.emit("OnBroadcastStateChanged", {
      txId: "aa",
      state: "Validated",
      updatedAtMs: 0,
      failReason: null,
    });
    expect(seen).toEqual(["Validated"]);

    off();
    bus.emit("OnBroadcastStateChanged", {
      txId: "aa",
      state: "Dispatching",
      updatedAtMs: 1,
      failReason: null,
    });
    expect(seen).toEqual(["Validated"]);
  });

  it("repeated subscribe/unsubscribe leaves zero listeners (no leak across navigation)", () => {
    const bus = new EventBus();
    for (let i = 0; i < 50; i++) {
      const off = bus.on("OnNewBlock", () => {});
      off();
    }
    expect(bus.listenerCount("OnNewBlock")).toBe(0);
  });

  it("SignalR client stop() halts emit cadence — even after many cycles", async () => {
    // S3-audit L1 fix: keep the subscription active AFTER stop()
    // so this case directly proves stop() clears the interval. If
    // the interval leaked, the counter would keep advancing on
    // every additional `advanceTimersByTime`.
    const bus = new EventBus();
    const tips: number[] = [];
    bus.on("OnNewBlock", (e) => tips.push(e.height));
    const client = new MockSignalRClient(bus);
    await client.start();
    vi.advanceTimersByTime(60_000); // 3 block intervals
    expect(tips.length).toBe(3);

    await client.stop();
    // Subscription stays active; if stop() forgot to clear the
    // interval the counter would advance below.
    vi.advanceTimersByTime(120_000);
    expect(tips.length).toBe(3);
  });

  it("same-instance double start() is a no-op (S3-audit M5 idempotent)", async () => {
    const bus = new EventBus();
    const tips: number[] = [];
    bus.on("OnNewBlock", (e) => tips.push(e.height));
    const client = new MockSignalRClient(bus);
    await client.start();
    await client.start(); // second start is a no-op
    vi.advanceTimersByTime(20_000);
    // Only one interval is running → one emit per 20s, not two.
    expect(tips.length).toBe(1);
    await client.stop();
  });

  it("subscribing AFTER stop() yields no events", async () => {
    const bus = new EventBus();
    const client = new MockSignalRClient(bus);
    await client.start();
    await client.stop();

    const tips: number[] = [];
    bus.on("OnNewBlock", (e) => tips.push(e.height));
    vi.advanceTimersByTime(120_000);
    expect(tips).toEqual([]);
  });

  it("starting two clients against the same bus does not duplicate-deliver if one is stopped", async () => {
    // Defends against the "double-mount in StrictMode" scenario by
    // proving that ONE client running is enough — the other's
    // timer must be cleared by stop().
    const bus = new EventBus();
    const tips: number[] = [];
    bus.on("OnNewBlock", (e) => tips.push(e.height));

    const a = new MockSignalRClient(bus);
    const b = new MockSignalRClient(bus);
    await a.start();
    await b.start();
    await a.stop(); // simulate cleanup of the first mount
    vi.advanceTimersByTime(20_000);

    // Only the surviving client b emits — one tip per 20s.
    expect(tips.length).toBe(1);

    await b.stop();
  });
});
