import { describe, expect, it, vi } from "vitest";
import { SetupStore } from "./setup.store";
import { MockAdminClient } from "@/lib/mock/admin";

describe("SetupStore (S7-S12-audit M2)", () => {
  it("loads the setup-status payload on start()", async () => {
    const store = new SetupStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.data?.adminEnabled).toBe(true);
    store.dispose();
  });

  it("captures errors", async () => {
    const admin = new MockAdminClient();
    vi.spyOn(admin, "getSetupStatus").mockRejectedValueOnce(new Error("nope"));
    const store = new SetupStore({ admin });
    await store.start();
    expect(store.status).toBe("error");
    expect(store.error).toBe("nope");
    store.dispose();
  });
});
