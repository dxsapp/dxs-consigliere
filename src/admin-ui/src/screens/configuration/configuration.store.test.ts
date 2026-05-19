import { describe, expect, it, vi } from "vitest";
import { ConfigurationStore } from "./configuration.store";
import { MockAdminClient } from "@/lib/mock/admin";
import type { IAdminClient } from "@/lib/admin/admin-client";

describe("ConfigurationStore (S7-S12-audit M2)", () => {
  it("loads providers payload on start()", async () => {
    const store = new ConfigurationStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.data?.config.effective).not.toBeNull();
    store.dispose();
  });

  it("captures network errors into store.error", async () => {
    const admin = new MockAdminClient();
    vi.spyOn(admin, "getProviders").mockRejectedValueOnce(new Error("boom"));
    const store = new ConfigurationStore({ admin });
    await store.start();
    expect(store.status).toBe("error");
    expect(store.error).toBe("boom");
    store.dispose();
  });

  it("dispose() aborts in-flight + ignores late resolutions", async () => {
    const signals: AbortSignal[] = [];
    const admin: Pick<IAdminClient, "getProviders"> = {
      getProviders: (signal) => {
        if (signal) signals.push(signal);
        return new Promise(() => {});
      },
    };
    const store = new ConfigurationStore({ admin: admin as IAdminClient });
    void store.start();
    store.dispose();
    expect(signals[0]?.aborted).toBe(true);
  });
});
