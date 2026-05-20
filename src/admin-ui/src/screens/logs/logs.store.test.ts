import { describe, expect, it, vi } from "vitest";
import { LogsStore, type LogEventDto } from "./logs.store";

interface FakeHub {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
  on(event: string, handler: (...args: unknown[]) => void): void;
  onclose(handler: (err?: unknown) => void): void;
  emit(entry: LogEventDto): void;
  triggerClose(err?: unknown): void;
  invocations: Array<{ method: string; args: unknown[] }>;
}

function makeHub(): FakeHub {
  const handlers = new Map<string, (...args: unknown[]) => void>();
  let onCloseHandler: ((err?: unknown) => void) | null = null;
  const invocations: FakeHub["invocations"] = [];
  return {
    start: vi.fn(async () => {}),
    stop: vi.fn(async () => {}),
    invoke: vi.fn(async (method: string, ...args: unknown[]) => {
      invocations.push({ method, args });
    }),
    on: vi.fn((event: string, handler: (...args: unknown[]) => void) => {
      handlers.set(event, handler);
    }),
    onclose: vi.fn((handler: (err?: unknown) => void) => {
      onCloseHandler = handler;
    }),
    emit(entry: LogEventDto) {
      const handler = handlers.get("OnLogEvent");
      handler?.(entry);
    },
    triggerClose(err?: unknown) {
      onCloseHandler?.(err);
    },
    invocations,
  };
}

describe("LogsStore", () => {
  it("start subscribes via SignalR with the current filters", async () => {
    const hub = makeHub();
    const store = new LogsStore({ buildConnection: async () => hub });
    store.setMinLevel("warning");
    store.setCategoryFilter("Bsv.P2p");

    await store.start();

    expect(store.status).toBe("live");
    expect(hub.invocations[0]).toEqual({
      method: "SubscribeToLogs",
      args: ["warning", "Bsv.P2p"],
    });
  });

  it("appends OnLogEvent payloads + re-sanitizes message + exception", async () => {
    const hub = makeHub();
    const store = new LogsStore({ buildConnection: async () => hub });
    await store.start();

    hub.emit({
      unixMs: 1,
      level: "information",
      category: "Tests",
      message: 'apiKey="leak"',
      exception: "Cookie: a=b",
    });

    expect(store.entries).toHaveLength(1);
    expect(store.entries[0].message).toContain('apiKey="***"');
    expect(store.entries[0].exception).toContain("Cookie: ***");
  });

  it("caps the visible buffer at the configured capacity", async () => {
    const hub = makeHub();
    const store = new LogsStore({ buildConnection: async () => hub, capacity: 3 });
    await store.start();

    for (let i = 0; i < 5; i++) {
      hub.emit({ unixMs: i, level: "information", category: "T", message: String(i), exception: null });
    }
    expect(store.entries.map((e) => e.message)).toEqual(["2", "3", "4"]);
  });

  it("status flips to error when the start invoke throws", async () => {
    const hub = makeHub();
    hub.invoke = vi.fn(async () => { throw new Error("hub refused"); });
    const store = new LogsStore({ buildConnection: async () => hub });
    await store.start();
    expect(store.status).toBe("error");
    expect(store.error).toBe("hub refused");
  });

  it("resubscribe clears the old entries BEFORE invoking, replays the filters, and keeps the new snapshot", async () => {
    // S4-audit M1: the hub's SubscribeToLogs emits the ring
    // snapshot via fire-and-forget SendAsync BEFORE returning.
    // If the store cleared `entries` AFTER the invoke promise
    // resolved (the bug codex caught), the snapshot frames
    // delivered during the await would be wiped. This test
    // simulates that ordering: the FakeHub `invoke` callback
    // emits a snapshot frame mid-call, then completes.
    const hub = makeHubWithSnapshotOnSubscribe([
      { unixMs: 100, level: "warning", category: "Bsv.P2p", message: "snapshot-A", exception: null },
      { unixMs: 101, level: "error", category: "Bsv.P2p", message: "snapshot-B", exception: null },
    ]);
    const store = new LogsStore({ buildConnection: async () => hub });
    await store.start();

    // Older live entry under the previous filter — must NOT
    // survive the resubscribe.
    hub.emit({ unixMs: 1, level: "information", category: "T", message: "stale", exception: null });
    expect(store.entries.some((e) => e.message === "stale")).toBe(true);

    store.setMinLevel("warning");
    await store.resubscribe();

    expect(store.entries.map((e) => e.message)).toEqual(["snapshot-A", "snapshot-B"]);
    const last = hub.invocations[hub.invocations.length - 1];
    expect(last.method).toBe("SubscribeToLogs");
    expect(last.args[0]).toBe("warning");
  });
});

/** Test helper: every `invoke("SubscribeToLogs", ...)` call
 *  fires the OnLogEvent handler with the supplied frames
 *  BEFORE returning, mirroring the SignalR hub's
 *  "snapshot-then-ack" sequence. */
function makeHubWithSnapshotOnSubscribe(snapshot: LogEventDto[]): FakeHub {
  const hub = makeHub();
  const originalInvoke = hub.invoke;
  hub.invoke = vi.fn(async (method: string, ...args: unknown[]) => {
    if (method === "SubscribeToLogs") {
      for (const frame of snapshot) hub.emit(frame);
    }
    return originalInvoke(method, ...args);
  });
  return hub;
}
