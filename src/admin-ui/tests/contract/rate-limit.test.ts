import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { callApi, startHost, type HostHandle } from "./_host-harness";

/**
 * wave-A3 S1 — burst the login endpoint against a dedicated
 * ASP.NET host spawned with STRICT rate-limit overrides.
 *
 * The shared (globalSetup) contract host runs with
 * Test-environment loose limits (10000/min) so the other 20
 * contract describes can pump traffic freely. This file
 * spawns a second host with `RateLimiting__Login__
 * PermitsPerMinute=5` so the slice's "10 attempts → 5 pass +
 * 5 fail with 429" behaviour can be exercised end-to-end.
 *
 * Spawn cost is ~10s startup; we amortise across both burst
 * describes below and tear the host down in afterAll.
 */
let strictHost: HostHandle;

beforeAll(async () => {
  strictHost = await startHost({
    envOverrides: {
      // 5 logins/min — matches the wave-A3 launch-prompt
      // conservative default the prod config carries.
      RateLimiting__Login__PermitsPerMinute: "5",
      // Loose `me` so the burst test below can independently
      // verify the login policy doesn't bleed into /me.
      RateLimiting__Me__PermitsPerMinute: "10000",
      RateLimiting__Broadcast__PermitsPerSecond: "10000",
    },
  });
});

afterAll(async () => {
  await strictHost.stop();
});

function anonymous(): HostHandle {
  return {
    baseUrl: strictHost.baseUrl,
    cookieJar: new Map<string, string>(),
    stop: async () => {},
  };
}

describe("wave-A3 S1 — login rate limiter", () => {
  it("10 logins in a tight burst → first 5 reach the auth layer, last 5 return 429 with Retry-After", async () => {
    const responses: Response[] = [];
    for (let i = 0; i < 10; i++) {
      responses.push(
        await callApi(anonymous(), "/api/admin/auth/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ username: "x", password: "y" }),
        }),
      );
    }
    const non429 = responses.filter((r) => r.status !== 429);
    const tooMany = responses.filter((r) => r.status === 429);

    // Wizard hasn't completed on this fresh host, so the auth
    // layer reports `setup_required` (409). The point of this
    // assertion is the LIMITER cutoff: exactly 5 hits make it
    // past the limiter; the 6th onwards are rejected at the
    // edge. The exact 409/200 split is auth's business.
    expect(non429.length).toBe(5);
    expect(tooMany.length).toBe(5);

    for (const r of tooMany) {
      const retryAfter = r.headers.get("retry-after");
      expect(retryAfter).not.toBeNull();
      expect(Number(retryAfter)).toBeGreaterThan(0);
    }
  });

  it("/api/admin/auth/me is on its own partition — login burst does NOT exhaust it", async () => {
    // The unit-test matrix pins partition-key namespacing in
    // process; this is the wire-level proof that the login
    // policy's exhaustion did not bleed into the /me policy.
    const res = await callApi(anonymous(), "/api/admin/auth/me");
    expect(res.status).toBe(200);
  });

  it("/health/live is opted out of the rate limiter via DisableRateLimiting", async () => {
    // 30 probes in tight succession: well past the strict
    // login bucket but health endpoints must not 429 — k8s
    // probes hammer them on purpose.
    for (let i = 0; i < 30; i++) {
      const res = await callApi(anonymous(), "/health/live");
      expect(res.status).toBe(200);
    }
  });
});
