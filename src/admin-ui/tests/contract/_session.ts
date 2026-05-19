import { callApi, startHost, type HostHandle } from "./_host-harness";

const SETUP_USERNAME = "contract-admin";
const SETUP_PASSWORD = "ContractTestA2!";
const SETUP_BLOCK_SUB = "contract-sub-id";

let hostPromise: Promise<HostHandle> | null = null;
let prepared = false;

/**
 * wave-A2 S2 — single-shot setup of the admin account against
 * the live backend host. Every contract spec calls this in
 * `beforeAll`. The first invocation lazily spawns the dotnet
 * host (vitest fork has `singleFork: true`, so this module memo
 * lives across every spec file), walks the setup wizard, and
 * signs in. Subsequent invocations short-circuit on the cached
 * host + the prepared flag.
 *
 * Returns the host handle (with a populated cookie jar after
 * the login round-trip) so specs can `callApi` directly.
 */
export async function ensureAdminSession(): Promise<HostHandle> {
  if (!hostPromise) {
    hostPromise = startHost();
    if (typeof process !== "undefined") {
      // Best-effort cleanup when the fork exits.
      process.on("beforeExit", () => {
        hostPromise?.then((h) => h.stop()).catch(() => {});
      });
    }
  }
  const host = await hostPromise;
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

/** Explicit teardown — registered with `afterAll` in any spec. */
export async function stopAdminSession(): Promise<void> {
  if (!hostPromise) return;
  const host = await hostPromise.catch(() => null);
  hostPromise = null;
  prepared = false;
  if (host) await host.stop();
}

async function completeSetupIfNeeded(host: HostHandle): Promise<void> {
  const status = await callApi(host, "/api/setup/status");
  if (!status.ok) {
    throw new Error(`/api/setup/status returned ${status.status}`);
  }
  const json = (await status.json()) as { setupCompleted?: boolean };
  if (json.setupCompleted === true) return;

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
