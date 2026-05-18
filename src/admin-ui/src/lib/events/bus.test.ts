import { describe, expect, it, vi } from "vitest";
import { EventBus } from "./bus";

describe("EventBus", () => {
  it("delivers a payload to a registered handler", () => {
    const bus = new EventBus();
    const handler = vi.fn();
    bus.on("OnNewBlock", handler);
    bus.emit("OnNewBlock", {
      hash: "aa",
      height: 1,
      timestampMs: 0,
      prevHash: "",
      headerSize: 80,
    });
    expect(handler).toHaveBeenCalledWith(
      expect.objectContaining({ hash: "aa", height: 1 })
    );
  });

  it("delivers to multiple subscribers; one throwing handler does not block the rest", () => {
    const bus = new EventBus();
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const ok = vi.fn();
    const throwing = vi.fn(() => {
      throw new Error("boom");
    });
    bus.on("OnReorg", throwing);
    bus.on("OnReorg", ok);
    bus.emit("OnReorg", {
      commonAncestorHash: "aa",
      commonAncestorHeight: 1,
      orphanedHashes: [],
      newTipHash: "bb",
      newTipHeight: 2,
      degradedState: false,
    });
    expect(throwing).toHaveBeenCalled();
    expect(ok).toHaveBeenCalled();
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("returns an unsubscribe fn that detaches the handler", () => {
    const bus = new EventBus();
    const handler = vi.fn();
    const off = bus.on("OnBroadcastStateChanged", handler);
    expect(bus.listenerCount("OnBroadcastStateChanged")).toBe(1);
    off();
    expect(bus.listenerCount("OnBroadcastStateChanged")).toBe(0);
    bus.emit("OnBroadcastStateChanged", {
      txId: "aa",
      state: "Validated",
      updatedAtMs: 0,
      failReason: null,
    });
    expect(handler).not.toHaveBeenCalled();
  });

  it("clears the per-key set when the last subscriber unsubscribes", () => {
    const bus = new EventBus();
    const off1 = bus.on("OnNewBlock", () => {});
    const off2 = bus.on("OnNewBlock", () => {});
    expect(bus.listenerCount("OnNewBlock")).toBe(2);
    off1();
    off2();
    expect(bus.listenerCount("OnNewBlock")).toBe(0);
  });

  it("reset() wipes all subscriptions", () => {
    const bus = new EventBus();
    bus.on("OnNewBlock", () => {});
    bus.on("OnReorg", () => {});
    expect(bus.listenerCount("OnNewBlock")).toBe(1);
    expect(bus.listenerCount("OnReorg")).toBe(1);
    bus.reset();
    expect(bus.listenerCount("OnNewBlock")).toBe(0);
    expect(bus.listenerCount("OnReorg")).toBe(0);
  });
});
