import { describe, expect, it } from "vitest";
import { MockAdminClient } from "@/lib/mock/admin";

/**
 * wave-A3 S3-audit L1 — pin the broadcast → audit-log wiring
 * in mock mode. The real backend's `BroadcastService` records
 * an audit entry BEFORE the broadcast lands; the mock now
 * mirrors that behaviour so the documented mock-mode smoke
 * ("trigger broadcast → see entry in /audit-log") is a real
 * round-trip and not a stale-seed display.
 */
describe("MockAdminClient broadcast → audit-log flow", () => {
  it("prepends a fresh audit entry with the txid that broadcastRaw just returned", async () => {
    const client = new MockAdminClient(() => 1_700_000_000_000);
    const receipt = await client.broadcastRaw("deadbeef00".repeat(8));
    const audit = await client.getAuditLog({ lastN: 5 });
    expect(audit.entries[0].targetId).toBe(receipt.txId);
    expect(audit.entries[0].action).toBe("broadcast_tx");
    expect(audit.entries[0].unixMs).toBe(receipt.createdAtMs);
  });

  it("stamps the rawHex length into the context payload without leaking the bytes", async () => {
    const client = new MockAdminClient(() => 1_700_000_000_000);
    const raw = "11".repeat(100);
    await client.broadcastRaw(raw);
    const audit = await client.getAuditLog({ lastN: 1 });
    const ctx = JSON.parse(audit.entries[0].context!);
    expect(ctx.rawHexLength).toBe(raw.length);
    expect(ctx.source).toBe("admin-ui");
    // Defense-in-depth: the rawHex itself must NOT appear in
    // the context payload (slice contract — no broadcast
    // payload leaks).
    expect(audit.entries[0].context).not.toContain(raw);
  });

  it("totalMatched grows by exactly one per broadcast", async () => {
    const client = new MockAdminClient(() => 1_700_000_000_000);
    const before = await client.getAuditLog({});
    await client.broadcastRaw("aa".repeat(8));
    const after = await client.getAuditLog({});
    expect(after.totalMatched).toBe(before.totalMatched + 1);
  });
});
