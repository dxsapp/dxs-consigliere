import { describe, expect, it } from "vitest";
import { TokensStore } from "./tokens.store";
import { MockAdminClient } from "@/lib/mock/admin";

describe("TokensStore", () => {
  it("loads the seeded tracked tokens and flips status to ready", async () => {
    const store = new TokensStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.tokens.length).toBeGreaterThan(0);
    store.dispose();
  });

  it("tracks a new token, clears the form, and shows it in the list", async () => {
    const store = new TokensStore({ admin: new MockAdminClient() });
    await store.start();
    const before = store.tokens.length;
    store.setFormTokenId("tok-new-0002");
    store.setFormSymbol("NEW");
    expect(store.canSubmit).toBe(true);
    const ok = await store.submit();
    expect(ok).toBe(true);
    expect(store.formTokenId).toBe("");
    expect(store.tokens.length).toBe(before + 1);
    expect(store.tokens.some((t) => t.symbol === "NEW")).toBe(true);
    store.dispose();
  });

  it("surfaces the backend error code when a duplicate is submitted", async () => {
    const store = new TokensStore({ admin: new MockAdminClient() });
    await store.start();
    const existing = store.tokens[0].tokenId;
    store.setFormTokenId(existing);
    const ok = await store.submit();
    expect(ok).toBe(false);
    expect(store.submitError).toBe("already_tracked");
    store.dispose();
  });
});
