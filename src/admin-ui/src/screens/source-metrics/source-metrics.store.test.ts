import { describe, expect, it } from "vitest";
import { SourceMetricsStore } from "./source-metrics.store";
import { MockAdminClient } from "@/lib/mock/admin";

describe("SourceMetricsStore", () => {
  it("derives a non-empty first-seen sparkline per source", async () => {
    const store = new SourceMetricsStore({ admin: new MockAdminClient(), lastN: 24 });
    await store.refresh();
    const series = store.firstSeenSeries("p2p");
    expect(series.length).toBeGreaterThan(0);
    expect(series.every((v) => v >= 0)).toBe(true);
    store.dispose();
  });

  it("exposes the latest observation counter per source", async () => {
    const store = new SourceMetricsStore({ admin: new MockAdminClient() });
    await store.refresh();
    const latest = store.latestForSource("p2p");
    expect(latest).not.toBeNull();
    expect(latest!.invObserved).toBeGreaterThan(0);
    store.dispose();
  });
});
