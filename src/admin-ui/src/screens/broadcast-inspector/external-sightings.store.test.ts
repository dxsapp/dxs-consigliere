import { describe, expect, it, vi } from "vitest";
import { ExternalSightingsStore } from "./external-sightings.store";
import type { ExternalSightingResponse } from "@/types/admin";

/**
 * Polls 3 explorers; each row stops independently the moment it's seen.
 * Timers are injected so we drive ticks manually (no real intervals).
 */

function makeStore(opts?: {
  getExternalSighting?: (txId: string, source: string) => Promise<ExternalSightingResponse>;
  maxAttempts?: number;
}) {
  const ticks: Record<string, () => void> = {};
  // setInterval is called once per source (immediate probe happens inline).
  let seq = 0;
  const handles: number[] = [];
  const getExternalSighting =
    opts?.getExternalSighting ??
    vi.fn(async (_txId: string, source: string) => ({ source, seen: false }));
  const admin = { getExternalSighting: vi.fn(getExternalSighting) };
  const store = new ExternalSightingsStore({
    admin,
    txId: "ab".repeat(32),
    maxAttempts: opts?.maxAttempts,
    setInterval: (cb) => {
      const id = ++seq;
      ticks[String(id)] = cb;
      handles.push(id);
      return id as unknown as ReturnType<typeof setInterval>;
    },
    clearInterval: (h) => {
      const idx = handles.indexOf(h as unknown as number);
      if (idx >= 0) handles.splice(idx, 1);
    },
  });
  return { store, admin, handles };
}

const flush = () => new Promise((r) => setTimeout(r, 0));

describe("ExternalSightingsStore", () => {
  it("starts a poller per source and surfaces three rows", async () => {
    const { store } = makeStore();
    store.start();
    await flush();
    expect(store.sightings.map((s) => s.source)).toEqual(["woc", "bitails", "junglebus"]);
  });

  it("marks a source seen and stops its poller (handle cleared)", async () => {
    const { store, handles } = makeStore({
      getExternalSighting: async (_txId, source) => ({ source, seen: source === "woc" }),
    });
    store.start();
    await flush();

    const woc = store.sightings.find((s) => s.source === "woc")!;
    expect(woc.seen).toBe(true);
    expect(woc.polling).toBe(false);
    // woc's interval was cleared; the other two remain.
    expect(handles.length).toBe(2);
  });

  it("keeps polling a source that isn't seen yet", async () => {
    const { store } = makeStore();
    store.start();
    await flush();
    const bitails = store.sightings.find((s) => s.source === "bitails")!;
    expect(bitails.seen).toBe(false);
    expect(bitails.polling).toBe(true);
  });

  it("gives up after maxAttempts and stops polling (not seen)", async () => {
    const { store, handles } = makeStore({ maxAttempts: 1 });
    store.start();
    await flush(); // the immediate probe is attempt #1 → hits maxAttempts
    expect(handles.length).toBe(0);
    expect(store.sightings.every((s) => !s.seen && !s.polling)).toBe(true);
  });

  it("dispose() clears all timers", async () => {
    const { store, handles } = makeStore();
    store.start();
    await flush();
    expect(handles.length).toBe(3);
    store.dispose();
    expect(handles.length).toBe(0);
  });
});
