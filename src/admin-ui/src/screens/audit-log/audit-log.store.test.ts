import { describe, expect, it, vi } from "vitest";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminAuditLogResponse } from "@/types/admin";
import { AuditLogStore } from "./audit-log.store";

function buildAdmin(getAuditLog: IAdminClient["getAuditLog"]): IAdminClient {
  return {
    getAuditLog,
    getP2pHealth: vi.fn(),
    getSourceMetrics: vi.fn(),
    getTrackedAddress: vi.fn(),
    getTrackedToken: vi.fn(),
    broadcastRaw: vi.fn(),
    getAlerts: vi.fn(),
    getPeers: vi.fn(),
    getHeadersTip: vi.fn(),
    getHeadersRecent: vi.fn(),
    getProviders: vi.fn(),
    getSetupStatus: vi.fn(),
    getSetupOptions: vi.fn(),
    completeSetup: vi.fn(),
  };
}

describe("AuditLogStore", () => {
  it("refresh loads entries + totalMatched and flips status to ready", async () => {
    const response: AdminAuditLogResponse = {
      totalMatched: 2,
      entries: [
        { id: "a", unixMs: 1, username: "u", action: "broadcast_tx", targetId: "t", context: "{}" },
        { id: "b", unixMs: 0, username: "v", action: "broadcast_tx", targetId: "t", context: null },
      ],
    };
    const admin = buildAdmin(vi.fn().mockResolvedValue(response));
    const store = new AuditLogStore({ admin });

    await store.refresh();

    expect(store.status).toBe("ready");
    expect(store.totalMatched).toBe(2);
    expect(store.entries).toHaveLength(2);
  });

  it("setActionFilter / setUsernameFilter / setSinceFilter only mutate the filter shape", () => {
    const store = new AuditLogStore({ admin: buildAdmin(vi.fn()) });
    store.setActionFilter("broadcast_tx");
    store.setUsernameFilter("admin");
    store.setSinceFilter("2026-01-01T00:00:00Z");
    expect(store.filter).toEqual({
      action: "broadcast_tx",
      username: "admin",
      since: "2026-01-01T00:00:00Z",
    });
  });

  it("translates an ISO-8601 since into unix-ms before forwarding to the client", async () => {
    const getAuditLog = vi.fn().mockResolvedValue({ totalMatched: 0, entries: [] });
    const store = new AuditLogStore({ admin: buildAdmin(getAuditLog) });
    store.setSinceFilter("2026-05-19T00:00:00Z");
    await store.refresh();
    const call = getAuditLog.mock.calls[0][0];
    expect(call.since).toBe(Date.parse("2026-05-19T00:00:00Z"));
  });

  it("aborts the previous inflight request when refresh is called again", async () => {
    // Resolve only the second call to make the test deterministic.
    const calls: Array<(value: AdminAuditLogResponse) => void> = [];
    const getAuditLog = vi.fn().mockImplementation(
      () => new Promise<AdminAuditLogResponse>((resolve) => calls.push(resolve)),
    );
    const store = new AuditLogStore({ admin: buildAdmin(getAuditLog) });

    const first = store.refresh();
    const second = store.refresh();
    // Drain the second call; the first one should have been
    // aborted via its controller (no state mutation).
    calls[1]({ totalMatched: 1, entries: [
      { id: "x", unixMs: 1, username: "u", action: "broadcast_tx", targetId: "t", context: null },
    ] });
    await second;
    // Now resolve the first one too; the store should ignore it.
    calls[0]({ totalMatched: 99, entries: [] });
    await first;

    expect(store.totalMatched).toBe(1);
  });

  it("surfaces an error message when the client rejects", async () => {
    const store = new AuditLogStore({
      admin: buildAdmin(vi.fn().mockRejectedValue({ message: "boom" })),
    });
    await store.refresh();
    expect(store.status).toBe("error");
    expect(store.error).toBe("boom");
  });
});
