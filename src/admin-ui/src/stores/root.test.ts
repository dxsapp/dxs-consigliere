import { describe, expect, it } from "vitest";
import { AuthStore } from "./root";
import { MockAuthClient } from "@/lib/mock/auth";

describe("AuthStore (S3 cookie-mode flow)", () => {
  it("starts idle then transitions to anonymous on hydrate", async () => {
    const auth = new AuthStore(new MockAuthClient());
    expect(auth.status).toBe("idle");
    await auth.hydrate();
    expect(auth.status).toBe("anonymous");
    expect(auth.isAuthenticated).toBe(false);
  });

  it("signIn with valid creds → authenticated; isAuthenticated true; user set", async () => {
    const auth = new AuthStore(new MockAuthClient());
    const ok = await auth.signIn({ username: "operator", password: "consigliere" });
    expect(ok).toBe(true);
    expect(auth.status).toBe("authenticated");
    expect(auth.isAuthenticated).toBe(true);
    expect(auth.user?.name).toBe("operator");
  });

  it("signIn with invalid creds → anonymous (NOT error), returns false", async () => {
    const auth = new AuthStore(new MockAuthClient());
    const ok = await auth.signIn({ username: "operator", password: "wrong" });
    expect(ok).toBe(false);
    // 401 is anonymous, not error (per applyError contract).
    expect(auth.status).toBe("anonymous");
    expect(auth.lastError).toBeNull();
  });

  it("signIn with empty creds → error with credentials_required", async () => {
    const auth = new AuthStore(new MockAuthClient());
    const ok = await auth.signIn({ username: "", password: "" });
    expect(ok).toBe(false);
    expect(auth.status).toBe("error");
    expect(auth.lastError).toContain("credentials_required");
  });

  it("signOut flips authenticated → anonymous", async () => {
    const auth = new AuthStore(new MockAuthClient());
    await auth.signIn({ username: "operator", password: "consigliere" });
    expect(auth.isAuthenticated).toBe(true);
    await auth.signOut();
    expect(auth.status).toBe("anonymous");
    expect(auth.user).toBeNull();
  });

  it("hydrate after signIn preserves authenticated state (cookie present)", async () => {
    const client = new MockAuthClient();
    const auth = new AuthStore(client);
    await auth.signIn({ username: "operator", password: "consigliere" });
    expect(auth.isAuthenticated).toBe(true);
    await auth.hydrate();
    expect(auth.isAuthenticated).toBe(true);
  });

  it("forceAuthenticatedForTests synchronously flips the store", () => {
    const auth = new AuthStore(new MockAuthClient());
    expect(auth.isAuthenticated).toBe(false);
    auth.forceAuthenticatedForTests("admin");
    expect(auth.isAuthenticated).toBe(true);
    expect(auth.user?.name).toBe("admin");
  });
});
