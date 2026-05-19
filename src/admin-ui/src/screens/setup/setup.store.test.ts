import { describe, expect, it, vi } from "vitest";
import { SetupStore } from "./setup.store";
import { MockAdminClient } from "@/lib/mock/admin";

describe("SetupStore (S7-S12-audit M2)", () => {
  it("loads the setup-status payload on start()", async () => {
    // wave-A2 S0: MockAdminClient.getSetupStatus now reads from
    // localStorage so the LoginPage banner + AuthGuard /setup
    // redirect can fire. Pre-seed a completed install for this
    // case — the store contract is "fetch + expose"; we're not
    // asserting wizard semantics here.
    window.localStorage.setItem(
      "consigliere-admin/mock-setup-state/v1",
      JSON.stringify({
        setupRequired: false,
        setupCompleted: true,
        adminEnabled: true,
        adminUsername: "operator",
      })
    );
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
