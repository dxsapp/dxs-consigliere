import { beforeAll, describe, expect, it } from "vitest";
import { callApi, type HostHandle } from "./_host-harness";
import { ensureAdminSession } from "./_session";

/**
 * wave-A3 S2 — runtime parity for the three anonymous health
 * probes. Reuses the contract host harness so we exercise the
 * actual `MapHealthChecks` wiring + the JSON response writer
 * defined in `Health/HealthResponseWriter.cs`.
 *
 * The harness boots a real Raven, so under nominal conditions
 * `/health/ready` returns 200; the slice contract for the
 * degraded path ("when Raven is stopped") is exercised by the
 * `RavenHealthCheck` unit test rather than here, because
 * tearing the Raven container down mid-suite would break every
 * other describe.
 */
let host: HostHandle;

beforeAll(async () => {
  host = await ensureAdminSession();
});

const ANONYMOUS = (): HostHandle => ({
  baseUrl: host.baseUrl,
  cookieJar: new Map<string, string>(),
  stop: async () => {},
});

interface HealthBody {
  status: string;
  checks: ReadonlyArray<{
    name: string;
    status: string;
    description?: string | null;
    durationMs: number;
  }>;
}

describe("health probe contract parity", () => {
  it("GET /health/live → 200 with empty checks array (no probes run)", async () => {
    const res = await callApi(ANONYMOUS(), "/health/live");
    expect(res.status).toBe(200);
    const body = (await res.json()) as HealthBody;
    expect(body.status).toBe("healthy");
    expect(Array.isArray(body.checks)).toBe(true);
    expect(body.checks.length).toBe(0);
  });

  it("GET /health/ready → 200 with the two `ready` checks", async () => {
    const res = await callApi(ANONYMOUS(), "/health/ready");
    expect([200, 503]).toContain(res.status);
    const body = (await res.json()) as HealthBody;
    const names = body.checks.map((c) => c.name).sort();
    expect(names).toEqual(["providers", "raven"]);
    // Raven is up under the test harness, so it MUST report
    // healthy. Providers may be degraded (no network reach to
    // bitails.io etc. in CI), so we don't constrain status.
    const raven = body.checks.find((c) => c.name === "raven");
    expect(raven?.status).toBe("healthy");
  });

  it("GET /health/startup → 200 with the di-graph check", async () => {
    const res = await callApi(ANONYMOUS(), "/health/startup");
    expect(res.status).toBe(200);
    const body = (await res.json()) as HealthBody;
    const names = body.checks.map((c) => c.name);
    expect(names).toEqual(["di-graph"]);
    expect(body.checks[0].status).toBe("healthy");
  });

  it("probes are reachable without a session cookie", async () => {
    // Anonymous + no Authorization header. The cookie-jar
    // factory above already strips the admin cookie; this is
    // belt-and-braces against a future middleware order change
    // that would gate /health behind UseAuthentication.
    const res = await callApi(ANONYMOUS(), "/health/live", { method: "GET" });
    expect(res.status).toBe(200);
  });
});
