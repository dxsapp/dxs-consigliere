import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { DashboardStore } from "./dashboard.store";
import { EventBus } from "@/lib/events/bus";
import { MockAdminClient } from "@/lib/mock/admin";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { P2pHealthDto, SourceMetricsResponse } from "@/types/admin";

/** Compose a partial IAdminClient stub with S5 endpoints filled by
 *  the MockAdminClient. The dashboard exercises only health +
 *  metrics; the rest must satisfy the interface but never run. */
function adminStub(overrides: Partial<IAdminClient>): IAdminClient {
  const mock = new MockAdminClient();
  return {
    getP2pHealth: mock.getP2pHealth.bind(mock),
    getSourceMetrics: mock.getSourceMetrics.bind(mock),
    getTrackedAddress: mock.getTrackedAddress.bind(mock),
    getTrackedToken: mock.getTrackedToken.bind(mock),
    broadcastRaw: mock.broadcastRaw.bind(mock),
    ...overrides,
  };
}

function build(opts?: {
  admin?: IAdminClient;
  healthPollMs?: number;
  metricsPollMs?: number;
}) {
  const bus = new EventBus();
  const admin = opts?.admin ?? new MockAdminClient();
  const store = new DashboardStore({
    admin,
    bus,
    healthPollMs: opts?.healthPollMs ?? 50,
    metricsPollMs: opts?.metricsPollMs ?? 50,
  });
  return { store, bus, admin };
}

describe("DashboardStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("hydrates health + metrics on start()", async () => {
    const { store } = build();
    await store.start();
    expect(store.health?.poolSize).toBe(8);
    expect(store.metrics?.history.length).toBeGreaterThan(0);
    expect(store.healthSummary.status).toBe("online");
    store.dispose();
  });

  it("derives a per-source rate from the metrics history", async () => {
    const { store } = build();
    await store.start();
    const rates = store.sourceRates;
    expect(rates).toHaveLength(3);
    const p2p = rates.find((r) => r.source === "p2p");
    expect(p2p?.ratePerMinute).toBeGreaterThan(0);
    store.dispose();
  });

  it("derives a non-empty sparkline series from N+1 snapshots", async () => {
    const { store } = build();
    await store.start();
    const series = store.mempoolRateSeries;
    expect(series.length).toBeGreaterThan(0);
    // Mock seed has growing FirstSeen values → all deltas non-negative.
    expect(series.every((v) => v >= 0)).toBe(true);
    store.dispose();
  });

  it("records recent broadcasts from the event bus newest-first (LRU)", async () => {
    const { store, bus } = build();
    await store.start();
    expect(store.recentBroadcasts).toEqual([]);
    bus.emit("OnBroadcastStateChanged", {
      txId: "aaaa",
      state: "Validated",
      updatedAtMs: 1,
      failReason: null,
    });
    bus.emit("OnBroadcastStateChanged", {
      txId: "bbbb",
      state: "Dispatching",
      updatedAtMs: 2,
      failReason: null,
    });
    expect(store.recentBroadcasts.map((b) => b.txId)).toEqual(["bbbb", "aaaa"]);
    store.dispose();
  });

  it("merges repeat broadcasts on the same txid (newest state wins, no duplicate)", async () => {
    const { store, bus } = build();
    await store.start();
    bus.emit("OnBroadcastStateChanged", {
      txId: "aaaa",
      state: "Validated",
      updatedAtMs: 1,
      failReason: null,
    });
    bus.emit("OnBroadcastStateChanged", {
      txId: "aaaa",
      state: "Dispatching",
      updatedAtMs: 2,
      failReason: null,
    });
    expect(store.recentBroadcasts).toHaveLength(1);
    expect(store.recentBroadcasts[0].state).toBe("Dispatching");
    store.dispose();
  });

  it("caps recent broadcasts at 12 entries (LRU)", async () => {
    const { store, bus } = build();
    await store.start();
    for (let i = 0; i < 20; i++) {
      bus.emit("OnBroadcastStateChanged", {
        txId: `tx${i.toString().padStart(2, "0")}`,
        state: "Validated",
        updatedAtMs: i,
        failReason: null,
      });
    }
    expect(store.recentBroadcasts).toHaveLength(12);
    // Newest first → last emitted is the head.
    expect(store.recentBroadcasts[0].txId).toBe("tx19");
  });

  it("dispose() halts polling AND removes the bus subscription", async () => {
    const calls: string[] = [];
    const recordingAdmin = adminStub({
      getP2pHealth: async () => {
        calls.push("health");
        return stubHealth();
      },
      getSourceMetrics: async () => {
        calls.push("metrics");
        return { latest: null, history: [] };
      },
    });
    const { store, bus } = build({ admin: recordingAdmin });
    await store.start();
    const callsAfterStart = calls.length;
    store.dispose();
    // Advance timers past several poll intervals — no new admin calls.
    vi.advanceTimersByTime(500);
    expect(calls.length).toBe(callsAfterStart);
    // Broadcasts emitted after dispose are ignored.
    bus.emit("OnBroadcastStateChanged", {
      txId: "post-dispose",
      state: "Validated",
      updatedAtMs: 0,
      failReason: null,
    });
    expect(store.recentBroadcasts.find((b) => b.txId === "post-dispose")).toBeUndefined();
  });

  it("start() is idempotent (StrictMode double-mount safe)", async () => {
    const calls: string[] = [];
    const admin = adminStub({
      getP2pHealth: async () => {
        calls.push("h");
        return stubHealth();
      },
      getSourceMetrics: async () => {
        calls.push("m");
        return { latest: null, history: [] };
      },
    });
    const { store } = build({ admin });
    await store.start();
    await store.start();
    // Two health + two metrics, NOT four (no extra timers attached).
    expect(calls.filter((c) => c === "h")).toHaveLength(1);
    expect(calls.filter((c) => c === "m")).toHaveLength(1);
    store.dispose();
  });

  it("reports degraded health when poolSize < target", async () => {
    const partial = adminStub({
      getP2pHealth: async () => ({
        bound: true,
        poolSize: 3,
        targetPoolSize: 8,
        subnet24Diversity: 3,
        activePeers: [],
        inboundEnabled: false,
      }),
      getSourceMetrics: async () => ({ latest: null, history: [] } as SourceMetricsResponse),
    });
    const { store } = build({ admin: partial });
    await store.start();
    expect(store.healthSummary.status).toBe("degraded");
    store.dispose();
  });

  it("reports offline when bound but pool is empty", async () => {
    const empty = adminStub({
      getP2pHealth: async () => ({
        bound: true,
        poolSize: 0,
        targetPoolSize: 8,
        subnet24Diversity: 0,
        activePeers: [],
        inboundEnabled: false,
      }),
      getSourceMetrics: async () => ({ latest: null, history: [] }),
    });
    const { store } = build({ admin: empty });
    await store.start();
    expect(store.healthSummary.status).toBe("offline");
    store.dispose();
  });

  it("captures error message when the admin client rejects", async () => {
    const broken = adminStub({
      getP2pHealth: async () => {
        throw new Error("ECONNREFUSED");
      },
      getSourceMetrics: async () => ({ latest: null, history: [] }),
    });
    const { store } = build({ admin: broken });
    await store.start();
    expect(store.healthStatus).toBe("error");
    expect(store.lastError).toContain("ECONNREFUSED");
    store.dispose();
  });
});

function stubHealth(): P2pHealthDto {
  return {
    bound: true,
    poolSize: 8,
    targetPoolSize: 8,
    subnet24Diversity: 6,
    activePeers: [],
    inboundEnabled: false,
  };
}

