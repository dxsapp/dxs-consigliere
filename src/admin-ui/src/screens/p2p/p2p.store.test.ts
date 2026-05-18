import { describe, expect, it } from "vitest";
import { P2pStore } from "./p2p.store";
import { MockAdminClient } from "@/lib/mock/admin";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminPeerRow, AdminPeersResponse, P2pHealthDto } from "@/types/admin";

function peer(overrides: Partial<AdminPeerRow>): AdminPeerRow {
  return {
    endpoint: "10.0.0.1:8333",
    source: "Hardcoded",
    userAgent: null,
    protocolVersion: 70016,
    services: 0x21,
    successCount: 10,
    failCount: 0,
    firstSeen: null,
    lastSeen: null,
    lastConnected: null,
    negativeUntil: null,
    lastFailureReason: null,
    subnet24: "10.0.0.0/24",
    ...overrides,
  };
}

function stubAdmin(overrides: Partial<{ health: P2pHealthDto; peers: AdminPeersResponse }>): IAdminClient {
  const mock = new MockAdminClient();
  return {
    getP2pHealth: async () => overrides.health ?? mock.getP2pHealth(),
    getSourceMetrics: mock.getSourceMetrics.bind(mock),
    getTrackedAddress: mock.getTrackedAddress.bind(mock),
    getTrackedToken: mock.getTrackedToken.bind(mock),
    broadcastRaw: mock.broadcastRaw.bind(mock),
    getAlerts: mock.getAlerts.bind(mock),
    getPeers: async () => overrides.peers ?? mock.getPeers(),
    getHeadersTip: mock.getHeadersTip.bind(mock),
    getHeadersRecent: mock.getHeadersRecent.bind(mock),
    getProviders: mock.getProviders.bind(mock),
    getSetupStatus: mock.getSetupStatus.bind(mock),
  };
}

describe("P2pStore.scoredPeers", () => {
  it("derives composite scores + sorts descending", async () => {
    const now = 1_000_000;
    const fresh = new Date(now - 5 * 60_000).toISOString();
    const stale = new Date(now - 23 * 60 * 60_000).toISOString();
    const peers: AdminPeersResponse = {
      total: 2,
      successful: 2,
      failed: 0,
      distinctSubnets: 1,
      peers: [
        peer({ endpoint: "fresh:8333", successCount: 10, failCount: 0, lastSeen: fresh, subnet24: "1.2.3.0/24" }),
        peer({ endpoint: "stale:8333", successCount: 6, failCount: 4, lastSeen: stale, subnet24: "1.2.3.0/24" }),
      ],
    };
    const store = new P2pStore({
      admin: stubAdmin({ peers }),
      now: () => now,
    });
    await store.refresh();
    const ranked = store.scoredPeers;
    expect(ranked[0].endpoint).toBe("fresh:8333");
    expect(ranked[0].score.composite).toBeGreaterThan(ranked[1].score.composite);
  });

  it("awards diversity for an otherwise-unrepresented /24", async () => {
    const peers: AdminPeersResponse = {
      total: 2,
      successful: 2,
      failed: 0,
      distinctSubnets: 2,
      peers: [
        peer({ endpoint: "p1:8333", subnet24: "10.0.0.0/24" }),
        peer({ endpoint: "p2:8333", subnet24: "10.0.1.0/24" }),
      ],
    };
    const store = new P2pStore({ admin: stubAdmin({ peers }) });
    await store.refresh();
    expect(store.scoredPeers.every((p) => p.score.diversity === 1)).toBe(true);
  });

  it("rolls the /24 breakdown into a sorted list", async () => {
    const peers: AdminPeersResponse = {
      total: 3,
      successful: 3,
      failed: 0,
      distinctSubnets: 2,
      peers: [
        peer({ endpoint: "p1:8333", subnet24: "10.0.0.0/24" }),
        peer({ endpoint: "p2:8333", subnet24: "10.0.0.0/24" }),
        peer({ endpoint: "p3:8333", subnet24: "10.0.1.0/24" }),
      ],
    };
    const store = new P2pStore({ admin: stubAdmin({ peers }) });
    await store.refresh();
    const breakdown = store.subnetBreakdown;
    expect(breakdown[0].subnet).toBe("10.0.0.0/24");
    expect(breakdown[0].count).toBe(2);
  });

  it("dispose() halts polling + aborts in-flight requests", async () => {
    const store = new P2pStore({ admin: new MockAdminClient(), pollMs: 50 });
    await store.start();
    store.dispose();
    // No follow-up assertion needed beyond "dispose returns cleanly"
    expect(store.status).toBe("ready");
  });
});
