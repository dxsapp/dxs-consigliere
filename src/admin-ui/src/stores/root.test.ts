import { describe, expect, it, vi } from "vitest";
import { AuthStore } from "./root";
import { MockAuthClient } from "@/lib/mock/auth";
import type { IAuthClient } from "@/lib/auth/client";
import type { AdminAuthStatusResponse } from "@/types/auth";
import { makeAppError } from "@/types/errors";

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

  it("signIn with invalid creds surfaces lastError (S3-audit M2)", async () => {
    const auth = new AuthStore(new MockAuthClient());
    const ok = await auth.signIn({ username: "operator", password: "wrong" });
    expect(ok).toBe(false);
    // S3-audit M2 fix: login 401 ≠ hydrate 401. The login path
    // ends in `anonymous` BUT surfaces `lastError` so the form
    // renders feedback.
    expect(auth.status).toBe("anonymous");
    expect(auth.lastError).toContain("invalid_credentials");
  });

  it("signIn with empty creds → anonymous + credentials_required error", async () => {
    const auth = new AuthStore(new MockAuthClient());
    const ok = await auth.signIn({ username: "", password: "" });
    expect(ok).toBe(false);
    expect(auth.status).toBe("anonymous");
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

  it("signOut clears user even when the network call fails (S3-audit H2)", async () => {
    const failingClient: IAuthClient = {
      me: () => Promise.resolve(stubStatus(true)),
      login: () => Promise.resolve(stubStatus(true)),
      logout: () => Promise.reject(makeAppError("Network", "ECONN_RESET")),
    };
    const auth = new AuthStore(failingClient);
    auth.forceAuthenticatedForTests("admin");
    expect(auth.isAuthenticated).toBe(true);
    await auth.signOut();
    expect(auth.status).toBe("anonymous");
    expect(auth.user).toBeNull();
    expect(auth.lastError).toContain("ECONN_RESET");
  });

  it("hydrate is idempotent (S3-audit M3) — concurrent calls share one client request", async () => {
    let inflightCalls = 0;
    const client: IAuthClient = {
      me: async () => {
        inflightCalls++;
        return stubStatus(false);
      },
      login: () => Promise.resolve(stubStatus(true)),
      logout: () => Promise.resolve(stubStatus(false)),
    };
    const auth = new AuthStore(client);
    const a = auth.hydrate();
    const b = auth.hydrate();
    expect(a).toBe(b); // same in-flight promise
    await Promise.all([a, b]);
    expect(inflightCalls).toBe(1);
    // Subsequent hydrate after the first completes starts a fresh
    // request (no permanent caching).
    await auth.hydrate();
    expect(inflightCalls).toBe(2);
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

  it("hydrate 401 → anonymous WITHOUT a user-facing error (session-expired path)", async () => {
    const unauthorizedClient: IAuthClient = {
      me: () => Promise.reject(makeAppError("Unauthorized", "no session", 401)),
      login: vi.fn(),
      logout: vi.fn(),
    } as IAuthClient;
    const auth = new AuthStore(unauthorizedClient);
    await auth.hydrate();
    expect(auth.status).toBe("anonymous");
    expect(auth.lastError).toBeNull();
  });
});

function stubStatus(authenticated: boolean): AdminAuthStatusResponse {
  return {
    setupRequired: false,
    enabled: true,
    authenticated,
    mode: "cookie",
    username: authenticated ? "operator" : "",
    sessionTtlMinutes: 60,
  };
}
