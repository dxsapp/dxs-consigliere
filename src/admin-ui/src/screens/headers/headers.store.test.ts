import { act } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HeadersStore } from "./headers.store";
import { EventBus } from "@/lib/events/bus";
import { MockAdminClient } from "@/lib/mock/admin";

describe("HeadersStore", () => {
  it("loads tip + recent on refresh", async () => {
    const store = new HeadersStore({
      admin: new MockAdminClient(),
      bus: new EventBus(),
      recentCount: 5,
    });
    await store.refresh();
    expect(store.tip?.height).toBeGreaterThan(0);
    expect(store.recent.length).toBeGreaterThan(0);
    store.dispose();
  });

  it("captures bus.OnReorg events into reorgEvents", async () => {
    const bus = new EventBus();
    const store = new HeadersStore({ admin: new MockAdminClient(), bus });
    await store.start();
    act(() => {
      bus.emit("OnReorg", {
        commonAncestorHash: "anc",
        commonAncestorHeight: 1,
        orphanedHashes: ["a", "b"],
        newTipHash: "newtip",
        newTipHeight: 3,
        degradedState: true,
      });
    });
    expect(store.reorgEvents.length).toBe(1);
    expect(store.reorgEvents[0].degradedState).toBe(true);
    store.dispose();
  });

  it("dispose() unwires bus subscription", async () => {
    const bus = new EventBus();
    const store = new HeadersStore({ admin: new MockAdminClient(), bus });
    await store.start();
    store.dispose();
    bus.emit("OnReorg", {
      commonAncestorHash: "anc",
      commonAncestorHeight: 1,
      orphanedHashes: [],
      newTipHash: "x",
      newTipHeight: 2,
      degradedState: false,
    });
    expect(store.reorgEvents.length).toBe(0);
  });
});
