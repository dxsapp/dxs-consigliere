import { inject } from "vitest";
import { callApi, type HostHandle } from "./_host-harness";

const SETUP_USERNAME = "contract-admin";
const SETUP_PASSWORD = "ContractTestA2!";
const SETUP_BLOCK_SUB = "contract-sub-id";

export const ADMIN_CREDENTIALS = Object.freeze({
  username: SETUP_USERNAME,
  password: SETUP_PASSWORD,
});

/** Well-known BSV address used as a fixture across the C# test
 *  suite (Dxs.Bsv.Tests, Dxs.Consigliere.Tests). */
export const SEEDED_ADDRESS = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

/** TokenId fixture shared with the DSTAS C# test suites. */
export const SEEDED_TOKEN_ID = "3333333333333333333333333333333333333333";

let seeded = false;

let host: HostHandle | null = null;
let prepared = false;
/** Captured by the first `completeSetupIfNeeded` call so M2
 *  tests can validate the shape returned by the wizard. */
let setupOptionsResponse: unknown = null;
let setupCompletionResponse: unknown = null;

/**
 * wave-A2 S2 — host shared across every spec in the fork via
 * `inject("contractHostBaseUrl")` (S2-audit H1 fix: vitest
 * globalSetup spawns + tears down the backend in the main
 * process; forks just consume the URL).
 *
 * The first call walks the setup wizard, signs in, and captures
 * the response bodies for shape-tests. Subsequent calls
 * short-circuit; if the cookie jar got cleared between specs
 * the helper re-signs in.
 */
export async function ensureAdminSession(): Promise<HostHandle> {
  if (!host) {
    const baseUrl = inject("contractHostBaseUrl");
    if (!baseUrl) {
      throw new Error("[contract] globalSetup did not provide contractHostBaseUrl");
    }
    host = {
      baseUrl,
      cookieJar: new Map<string, string>(),
      // The fork doesn't own the backend lifetime — globalTeardown does.
      stop: async () => {},
    };
  }
  if (!prepared) {
    await completeSetupIfNeeded(host);
    await loginAdmin(host);
    prepared = true;
  } else if (host.cookieJar.size === 0) {
    // We're past setup but the cookie jar got cleared between
    // specs — re-login.
    await loginAdmin(host);
  }
  return host;
}

/** Captured `GET /api/setup/options` body — for S2-audit M2
 *  validation. */
export function setupOptionsBody(): unknown {
  return setupOptionsResponse;
}

/** Captured `POST /api/setup/complete` body. */
export function setupCompletionBody(): unknown {
  return setupCompletionResponse;
}

async function completeSetupIfNeeded(host: HostHandle): Promise<void> {
  // S2-audit M2: pull options FIRST so a spec can shape-check
  // the wizard payload before submit completes.
  const optionsRes = await callApi(host, "/api/setup/options");
  if (!optionsRes.ok) {
    throw new Error(`/api/setup/options returned ${optionsRes.status}`);
  }
  setupOptionsResponse = await optionsRes.json();

  const statusJson = (setupOptionsResponse as { status?: { setupCompleted?: boolean } })
    .status;
  if (statusJson?.setupCompleted === true) {
    // Re-running the contract suite against an already-completed
    // RavenDB. Skip wizard submit; login still runs.
    setupCompletionResponse = statusJson;
    return;
  }

  const body = {
    admin: {
      enabled: true,
      username: SETUP_USERNAME,
      password: SETUP_PASSWORD,
    },
    providers: {
      rawTxPrimaryProvider: "junglebus",
      restFallbackProvider: "whatsonchain",
      realtimePrimaryProvider: "bitails",
      bitailsTransport: "websocket",
      bitails: {
        apiKey: "",
        baseUrl: "https://api.bitails.io",
        websocketBaseUrl: "https://api.bitails.io/global",
        zmqTxUrl: "",
        zmqBlockUrl: "",
      },
      whatsonchain: {
        apiKey: "",
        baseUrl: "https://api.whatsonchain.com/v1/bsv/main",
      },
      junglebus: {
        apiKey: "",
        baseUrl: "https://junglebus.gorillapool.io",
        mempoolSubscriptionId: "",
        blockSubscriptionId: SETUP_BLOCK_SUB,
      },
      node: { zmqTxUrl: "", zmqBlockUrl: "" },
    },
    blockSync: {
      baseUrl: "https://junglebus.gorillapool.io",
      blockSubscriptionId: SETUP_BLOCK_SUB,
    },
  };
  const res = await callApi(host, "/api/setup/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`/api/setup/complete failed (${res.status}): ${text}`);
  }
  // S2-audit M2 fix: capture the body so a spec can validate
  // the SetupStatusResponse shape that the backend returns.
  setupCompletionResponse = await res.json();
}

/**
 * S2-audit H2: seed a single tracked address + tracked token via
 * the real `POST /api/admin/manage/*` endpoints so the contract
 * suite can shape-check the *populated* list arrays AND the
 * detail endpoints the UI actually consumes
 * (`/api/admin/tracked/address/{address}` +
 * `/api/admin/tracked/token/{tokenId}`). Idempotent — re-running
 * against an already-seeded DB short-circuits on 409.
 */
export async function ensureSeededEntities(host: HostHandle): Promise<void> {
  if (seeded) return;
  await seedAddress(host);
  await seedToken(host);
  seeded = true;
}

async function seedAddress(host: HostHandle): Promise<void> {
  const res = await callApi(host, "/api/admin/manage/address", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      address: SEEDED_ADDRESS,
      name: "contract-fixture",
      historyPolicy: { mode: "forward_only" },
    }),
  });
  // 409 = already registered from a prior run against the same
  // Raven DB; treat as success.
  if (res.status === 409) return;
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`/api/admin/manage/address failed (${res.status}): ${text}`);
  }
}

async function seedToken(host: HostHandle): Promise<void> {
  const res = await callApi(host, "/api/admin/manage/stas-token", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      tokenId: SEEDED_TOKEN_ID,
      symbol: "FIXTURE",
      historyPolicy: { mode: "forward_only" },
    }),
  });
  if (res.status === 409) return;
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`/api/admin/manage/stas-token failed (${res.status}): ${text}`);
  }
}

async function loginAdmin(host: HostHandle): Promise<void> {
  const res = await callApi(host, "/api/admin/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username: SETUP_USERNAME, password: SETUP_PASSWORD }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`/api/admin/auth/login failed (${res.status}): ${text}`);
  }
}
