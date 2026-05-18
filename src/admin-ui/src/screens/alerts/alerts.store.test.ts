import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AlertsStore } from "./alerts.store";
import { MockAdminClient } from "@/lib/mock/admin";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { P2pAlertEventDto, P2pAlertResponse } from "@/types/admin";

function alertEvent(overrides: Partial<P2pAlertEventDto>): P2pAlertEventDto {
  return {
    id: "x",
    alertUnixMs: 1_000,
    type: "PoolSizeBelowThreshold",
    detail: "test",
    context: {},
    ...overrides,
  };
}

describe("AlertsStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("hydrates from the admin client on start()", async () => {
    const admin = new MockAdminClient(() => 1_000_000);
    const store = new AlertsStore({ admin, pollMs: 60_000, now: () => 1_000_000 });
    await store.start();
    expect(store.alerts.length).toBeGreaterThan(0);
    expect(store.status).toBe("ready");
    store.dispose();
  });

  it("advances the cursor on the first poll + uses it on the next", async () => {
    const calls: number[] = [];
    let now = 10_000;
    const admin: Partial<IAdminClient> = {
      getAlerts: async ({ since } = {}) => {
        calls.push(since ?? 0);
        return {
          alerts: [alertEvent({ id: `e-${calls.length}`, alertUnixMs: now })],
        } as P2pAlertResponse;
      },
    };
    const store = new AlertsStore({
      admin: admin as IAdminClient,
      pollMs: 60_000,
      now: () => now,
    });
    await store.start();
    expect(calls[0]).toBe(0);
    now = 20_000;
    await store.refresh();
    // Second call uses the cursor (== first event's alertUnixMs).
    expect(calls[1]).toBe(10_000);
    store.dispose();
  });

  it("dedupes by id on overlapping pages", async () => {
    let now = 5_000;
    const admin: Partial<IAdminClient> = {
      getAlerts: async () =>
        ({ alerts: [alertEvent({ id: "dup", alertUnixMs: 1_000 })] } as P2pAlertResponse),
    };
    const store = new AlertsStore({ admin: admin as IAdminClient, now: () => now });
    await store.start();
    now = 10_000;
    await store.refresh();
    expect(store.alerts.length).toBe(1);
    store.dispose();
  });

  it("classifies alerts by activeWindowMs cutoff", async () => {
    const now = 1_000_000;
    const admin: Partial<IAdminClient> = {
      getAlerts: async () =>
        ({
          alerts: [
            alertEvent({ id: "recent", alertUnixMs: now - 60_000 }),
            alertEvent({ id: "old", alertUnixMs: now - 60 * 60 * 1000 }),
          ],
        } as P2pAlertResponse),
    };
    const store = new AlertsStore({
      admin: admin as IAdminClient,
      activeWindowMs: 30 * 60 * 1000,
      now: () => now,
    });
    await store.start();
    expect(store.activeAlerts.map((a) => a.id)).toEqual(["recent"]);
    expect(store.historyAlerts.map((a) => a.id)).toEqual(["old"]);
    store.dispose();
  });

  it("consumeNewAlerts drains the delta queue", async () => {
    const now = 1_000;
    const admin: Partial<IAdminClient> = {
      getAlerts: async () =>
        ({
          alerts: [alertEvent({ id: "fresh", alertUnixMs: now })],
        } as P2pAlertResponse),
    };
    const store = new AlertsStore({ admin: admin as IAdminClient, now: () => now });
    await store.start();
    const delta = store.consumeNewAlerts();
    expect(delta.map((a) => a.id)).toEqual(["fresh"]);
    expect(store.consumeNewAlerts()).toHaveLength(0);
    store.dispose();
  });

  it("captures network errors into store.error", async () => {
    const admin: Partial<IAdminClient> = {
      getAlerts: async () => {
        throw new Error("net down");
      },
    };
    const store = new AlertsStore({ admin: admin as IAdminClient });
    await store.start();
    expect(store.status).toBe("error");
    expect(store.error).toBe("net down");
    store.dispose();
  });

  it("dispose() halts polling", async () => {
    const calls: number[] = [];
    const admin: Partial<IAdminClient> = {
      getAlerts: async () => {
        calls.push(1);
        return { alerts: [] } as P2pAlertResponse;
      },
    };
    const store = new AlertsStore({
      admin: admin as IAdminClient,
      pollMs: 100,
    });
    await store.start();
    store.dispose();
    const before = calls.length;
    vi.advanceTimersByTime(1_000);
    expect(calls.length).toBe(before);
  });
});
