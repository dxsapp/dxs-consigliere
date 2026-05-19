import { beforeAll, describe, expect, it } from "vitest";
import { callApi, type HostHandle } from "./_host-harness";
import { expectShape } from "./_schema-validator";
import { ensureAdminSession, ensureSeededEntities, SEEDED_ADDRESS, SEEDED_TOKEN_ID } from "./_session";

/**
 * wave-A2 S2 — admin REST contract parity. One describe block per
 * endpoint the admin UI consumes. Every response body must
 * structurally satisfy the Swashbuckle-emitted schema in
 * `contracts/swagger.json`. The wave-A1 S4-S6 audit H1 + H2 drift
 * defects (TrackedHistoryStatusResponse field rename,
 * AdminTrackedTokenBalanceSummaryResponse field reduction) would
 * each have failed this gate red on commit, instead of months later
 * via a screen rendering "undefined · NaN".
 */
let host: HostHandle;

beforeAll(async () => {
  host = await ensureAdminSession();
  // S2-audit H2: seed one tracked address + one tracked token so
  // both the list AND the detail endpoints have real bodies for
  // `expectShape` — the empty-array baseline never exercised the
  // DTO shapes the UI actually consumes.
  await ensureSeededEntities(host);
});

describe("admin REST contract parity", () => {
  it("GET /api/admin/p2p/health → P2pHealthDto", async () => {
    const res = await callApi(host, "/api/admin/p2p/health");
    expect(res.status).toBe(200);
    expectShape("P2pHealthDto", await res.json());
  });

  it("GET /api/admin/p2p/peers → object (anonymous response — only field-presence check)", async () => {
    // The peers endpoint returns an anonymous object literal
    // (not a sealed DTO), so Swashbuckle types it as `object`.
    // We at least confirm the shape has the documented fields.
    const res = await callApi(host, "/api/admin/p2p/peers");
    expect(res.status).toBe(200);
    const body = (await res.json()) as Record<string, unknown>;
    for (const key of ["total", "successful", "failed", "distinctSubnets", "peers"]) {
      expect(body).toHaveProperty(key);
    }
  });

  it("GET /api/admin/p2p/headers/tip → HeadersTipDto OR 404", async () => {
    const res = await callApi(host, "/api/admin/p2p/headers/tip");
    if (res.status === 404) return; // expected pre-bootstrap
    expect(res.status).toBe(200);
    expectShape("HeadersTipDto", await res.json());
  });

  it("GET /api/admin/p2p/headers/recent?count=N → HeadersTipDto[]", async () => {
    const res = await callApi(host, "/api/admin/p2p/headers/recent?count=5");
    expect(res.status).toBe(200);
    const body = (await res.json()) as unknown[];
    expect(Array.isArray(body)).toBe(true);
    for (const entry of body) {
      expectShape("HeadersTipDto", entry);
    }
  });

  it("GET /api/admin/p2p/alerts → P2pAlertResponse", async () => {
    const res = await callApi(host, "/api/admin/p2p/alerts");
    expect(res.status).toBe(200);
    expectShape("P2pAlertResponse", await res.json());
  });

  it("GET /api/admin/metrics/sources → SourceMetricsResponse", async () => {
    const res = await callApi(host, "/api/admin/metrics/sources?lastN=3");
    expect(res.status).toBe(200);
    expectShape("SourceMetricsResponse", await res.json());
  });

  it("GET /api/admin/providers → AdminProvidersResponse", async () => {
    const res = await callApi(host, "/api/admin/providers");
    expect(res.status).toBe(200);
    expectShape("AdminProvidersResponse", await res.json());
  });

  // S2-audit H2: with a seeded address the list array is non-
  // empty so `expectShape` actually runs on the DTO the UI binds.
  it("GET /api/admin/tracked/addresses → AdminTrackedAddressResponse[]", async () => {
    const res = await callApi(host, "/api/admin/tracked/addresses?includeTombstoned=false");
    expect(res.status).toBe(200);
    const body = (await res.json()) as unknown[];
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThan(0);
    for (const entry of body) {
      expectShape("AdminTrackedAddressResponse", entry);
    }
  });

  // S2-audit H2: detail endpoint is what `admin-client.ts`
  // consumes when the operator opens a tracked-address row.
  it("GET /api/admin/tracked/address/{address} → AdminTrackedAddressResponse", async () => {
    const res = await callApi(
      host,
      `/api/admin/tracked/address/${encodeURIComponent(SEEDED_ADDRESS)}`,
    );
    expect(res.status).toBe(200);
    expectShape("AdminTrackedAddressResponse", await res.json());
  });

  it("GET /api/admin/tracked/tokens → AdminTrackedTokenResponse[]", async () => {
    const res = await callApi(host, "/api/admin/tracked/tokens?includeTombstoned=false");
    expect(res.status).toBe(200);
    const body = (await res.json()) as unknown[];
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThan(0);
    for (const entry of body) {
      expectShape("AdminTrackedTokenResponse", entry);
    }
  });

  it("GET /api/admin/tracked/token/{tokenId} → AdminTrackedTokenResponse", async () => {
    const res = await callApi(
      host,
      `/api/admin/tracked/token/${encodeURIComponent(SEEDED_TOKEN_ID)}`,
    );
    expect(res.status).toBe(200);
    expectShape("AdminTrackedTokenResponse", await res.json());
  });

  it("GET /api/setup/status → SetupStatusResponse (setupCompleted=true after the wizard)", async () => {
    const res = await callApi(host, "/api/setup/status");
    expect(res.status).toBe(200);
    const body = await res.json();
    expectShape("SetupStatusResponse", body);
    expect(body.setupCompleted).toBe(true);
  });
});
