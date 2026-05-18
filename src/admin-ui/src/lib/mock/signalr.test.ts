import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EventBus, type EventMap } from "@/lib/events/bus";
import { MockSignalRClient } from "./signalr";

describe("MockSignalRClient", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("emits ConnectionStateChanged online on start, offline on stop", async () => {
    const bus = new EventBus();
    const states: EventMap["ConnectionStateChanged"]["status"][] = [];
    bus.on("ConnectionStateChanged", (e) => states.push(e.status));

    const client = new MockSignalRClient(bus);
    await client.start();
    expect(states).toEqual(["online"]);

    await client.stop();
    expect(states).toEqual(["online", "offline"]);
  });

  it("emits a block tip every 20 s and advances height by 1", async () => {
    const bus = new EventBus();
    const tips: number[] = [];
    bus.on("OnNewBlock", (e) => tips.push(e.height));

    const client = new MockSignalRClient(bus);
    await client.start();
    vi.advanceTimersByTime(20_000);
    vi.advanceTimersByTime(20_000);
    await client.stop();

    expect(tips.length).toBe(2);
    expect(tips[1]).toBe(tips[0] + 1);
  });

  it("emits broadcast-state transitions every 8 s, cycling 4 states", async () => {
    const bus = new EventBus();
    const states: string[] = [];
    bus.on("OnBroadcastStateChanged", (e) => states.push(e.state));

    const client = new MockSignalRClient(bus);
    await client.start();
    for (let i = 0; i < 4; i++) vi.advanceTimersByTime(8_000);
    await client.stop();

    expect(states).toEqual(["Validated", "Dispatching", "PeerRelayed", "Mined"]);
  });

  it("emitReorg() injects an OnReorg event on demand", async () => {
    const bus = new EventBus();
    const reorgs: EventMap["OnReorg"][] = [];
    bus.on("OnReorg", (e) => reorgs.push(e));

    const client = new MockSignalRClient(bus);
    await client.start();
    client.emitReorg();
    await client.stop();

    expect(reorgs.length).toBe(1);
    expect(reorgs[0].degradedState).toBe(false);
  });

  it("stop() stops both timers — no further events after teardown", async () => {
    const bus = new EventBus();
    const tips: number[] = [];
    bus.on("OnNewBlock", (e) => tips.push(e.height));

    const client = new MockSignalRClient(bus);
    await client.start();
    vi.advanceTimersByTime(20_000);
    expect(tips.length).toBe(1);
    await client.stop();
    vi.advanceTimersByTime(60_000);
    expect(tips.length).toBe(1);
  });
});
