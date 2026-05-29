import { describe, expect, it } from "vitest";
import { AddressesStore, errorMessage } from "./addresses.store";
import { MockAdminClient } from "@/lib/mock/admin";

describe("AddressesStore", () => {
  it("loads the seeded tracked addresses and flips status to ready", async () => {
    const store = new AddressesStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.addresses.length).toBeGreaterThan(0);
    store.dispose();
  });

  it("tracks a new address, clears the form, and shows it in the list", async () => {
    const store = new AddressesStore({ admin: new MockAdminClient() });
    await store.start();
    const before = store.addresses.length;
    store.setFormAddress("1NewlyTracked000000000000000000000");
    store.setFormName("Treasury");
    expect(store.canSubmit).toBe(true);
    const ok = await store.submit();
    expect(ok).toBe(true);
    expect(store.formAddress).toBe("");
    expect(store.addresses.length).toBe(before + 1);
    expect(store.addresses.some((a) => a.name === "Treasury")).toBe(true);
    store.dispose();
  });

  it("surfaces the backend error code when a duplicate is submitted", async () => {
    const admin = new MockAdminClient();
    const store = new AddressesStore({ admin });
    await store.start();
    const existing = store.addresses[0].address;
    store.setFormAddress(existing);
    const ok = await store.submit();
    expect(ok).toBe(false);
    expect(store.submitError).toBe("already_tracked");
    store.dispose();
  });

  it("errorMessage extracts a JSON {code} body", () => {
    expect(errorMessage({ message: '{"code":"invalid_address"}' })).toBe("invalid_address");
    expect(errorMessage({ message: "boom" })).toBe("boom");
    expect(errorMessage(undefined)).toBe("Unknown error");
  });
});
