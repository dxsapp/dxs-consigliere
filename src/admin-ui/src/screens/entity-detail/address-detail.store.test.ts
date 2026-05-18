import { describe, expect, it, vi } from "vitest";
import { AddressDetailStore } from "./address-detail.store";
import { MockAdminClient } from "@/lib/mock/admin";
import type { IAdminClient } from "@/lib/admin/admin-client";

describe("AddressDetailStore", () => {
  it("loads the tracked-address response and exposes it on store", async () => {
    const admin = new MockAdminClient();
    const store = new AddressDetailStore({ admin, address: "1abc" });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.data?.address).toBe("1abc");
    expect(store.error).toBeNull();
    store.dispose();
  });

  it("captures network errors into store.error", async () => {
    const admin = new MockAdminClient();
    vi.spyOn(admin, "getTrackedAddress").mockRejectedValueOnce(new Error("boom"));
    const store = new AddressDetailStore({ admin, address: "1abc" });
    await store.start();
    expect(store.status).toBe("error");
    expect(store.error).toBe("boom");
    store.dispose();
  });

  it("aborts an in-flight request when dispose() is called", async () => {
    const ctlRef: { current: AbortSignal | null } = { current: null };
    const admin: Pick<IAdminClient, "getTrackedAddress"> = {
      getTrackedAddress: (_address, signal) => {
        ctlRef.current = signal ?? null;
        return new Promise(() => {
          /* never resolves */
        });
      },
    };
    const store = new AddressDetailStore({
      admin: admin as IAdminClient,
      address: "1abc",
    });
    void store.start();
    store.dispose();
    expect(ctlRef.current?.aborted).toBe(true);
  });

  it("ignores empty address with a clear error message", async () => {
    const admin = new MockAdminClient();
    const spy = vi.spyOn(admin, "getTrackedAddress");
    const store = new AddressDetailStore({ admin, address: "" });
    await store.start();
    expect(spy).not.toHaveBeenCalled();
    expect(store.status).toBe("error");
    expect(store.error).toMatch(/missing address/);
    store.dispose();
  });
});
