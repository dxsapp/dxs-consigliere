import { beforeAll, describe, expect, it } from "vitest";
import { callApi, type HostHandle } from "./_host-harness";
import { expectShape } from "./_schema-validator";
import {
  ADMIN_CREDENTIALS,
  ensureAdminSession,
  setupCompletionBody,
  setupOptionsBody,
} from "./_session";

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

describe("setup contract parity", () => {
  // S2-audit M2: the wizard's GET /api/setup/options is the
  // *first* wire the UI hits; without a shape gate, a backend
  // rename of any defaults / allowed-options key would not
  // surface until the wizard renders.
  it("GET /api/setup/options matches SetupOptionsResponse", () => {
    const body = setupOptionsBody();
    expect(body).not.toBeNull();
    expectShape("SetupOptionsResponse", body);
  });

  // S2-audit M2: POST /api/setup/complete returns a
  // SetupStatusResponse — captured during session setup so we
  // don't have to seed a second admin.
  it("POST /api/setup/complete returns SetupStatusResponse", () => {
    const body = setupCompletionBody();
    expect(body).not.toBeNull();
    expectShape("SetupStatusResponse", body);
  });
});

describe("admin auth contract parity", () => {
  it("GET /api/admin/auth/me matches AdminAuthStatusResponse", async () => {
    const res = await callApi(host, "/api/admin/auth/me");
    expect(res.status).toBe(200);
    const body = await res.json();
    expectShape("AdminAuthStatusResponse", body);
    expect(body.authenticated).toBe(true);
  });

  // S2-audit M1: POST /api/admin/auth/login is fired during
  // session setup but the response body is discarded. Pin the
  // wire shape via a fresh anonymous round-trip (empty cookie
  // jar means we exercise the login path, not a cookie-renew).
  it("POST /api/admin/auth/login returns AdminAuthStatusResponse", async () => {
    const anonymous: HostHandle = {
      baseUrl: host.baseUrl,
      cookieJar: new Map<string, string>(),
      stop: async () => {},
    };
    const res = await callApi(anonymous, "/api/admin/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(ADMIN_CREDENTIALS),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expectShape("AdminAuthStatusResponse", body);
    expect(body.authenticated).toBe(true);
    expect(body.username).toBe(ADMIN_CREDENTIALS.username);
  });
});
