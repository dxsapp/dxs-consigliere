import { beforeAll, describe, expect, it } from "vitest";
import { callApi, type HostHandle } from "./_host-harness";
import { expectShape } from "./_schema-validator";
import { ensureAdminSession } from "./_session";

/**
 * wave-A2 S2 — admin auth contract parity. Boots a real ASP.NET
 * host (globalSetup), walks the setup wizard to create an admin
 * account, then exercises the auth surface end-to-end. Every
 * response body is shape-checked against the Swashbuckle-emitted
 * `components.schemas.<DTO>` from `contracts/swagger.json`.
 */
let host: HostHandle;

beforeAll(async () => {
  host = await ensureAdminSession();
});

describe("admin auth contract parity", () => {
  it("GET /api/admin/auth/me matches AdminAuthStatusResponse", async () => {
    const res = await callApi(host, "/api/admin/auth/me");
    expect(res.status).toBe(200);
    const body = await res.json();
    expectShape("AdminAuthStatusResponse", body);
    expect(body.authenticated).toBe(true);
  });

  it("returns AdminAuthStatusResponse for an authenticated GET /me round-trip", async () => {
    const res = await callApi(host, "/api/admin/auth/me");
    expect(res.status).toBe(200);
    const body = await res.json();
    expectShape("AdminAuthStatusResponse", body);
  });
});
