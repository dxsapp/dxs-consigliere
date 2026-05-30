import { beforeEach, describe, expect, it, vi } from "vitest";
import { LabStore, type LabStoreOptions } from "./lab.store";
import { MockAdminClient } from "@/lib/mock/admin";
import type { LabKey } from "@/screens/lab/lab.tx";

/**
 * tx-lab S1 store unit test.
 *
 * What is REAL here:
 *  - the LabStore orchestration (generate → trackAddress → refresh,
 *    send → buildSend → broadcastRawTx → refresh, error surfacing)
 *  - the MockAdminClient (real wire shapes: trackAddress,
 *    getAddressUtxos seed, broadcastRawTx receipt).
 *
 * What is MOCKED:
 *  - the in-browser SDK crypto wrapper (`generateLabKey` /
 *    `buildP2pkhSend` from lab.tx.ts) is injected via the store's
 *    constructor seam, so the elliptic-curve SDK never runs under
 *    jsdom. We assert the store calls the wrapper + forwards its
 *    rawHex to the admin client — not the SDK's signing math (that is
 *    covered by the SDK's own tests + the install-time smoke).
 */

const FAKE_KEY: LabKey = {
  address: "1LabAddrFake0000000000000000000000",
  wif: "KwFakeWifNeverLeavesTheBrowser0000000000000000000000",
  _privHex: "11".repeat(32),
};

// No-op timers so auto-poll never starts a real interval under jsdom.
function noopTimers(): Pick<LabStoreOptions, "setInterval" | "clearInterval"> {
  return {
    setInterval: () => 0 as unknown as ReturnType<typeof setInterval>,
    clearInterval: () => {},
  };
}

function makeStore(opts?: { buildSend?: LabStoreOptions["buildSend"] }) {
  const admin = new MockAdminClient();
  const trackSpy = vi.spyOn(admin, "trackAddress");
  const utxosSpy = vi.spyOn(admin, "getAddressUtxos");
  const broadcastSpy = vi.spyOn(admin, "broadcastRawTx");
  const generateKey = vi.fn(async () => FAKE_KEY);
  const buildSend =
    opts?.buildSend ??
    vi.fn(async () => ({ rawHex: "deadbeef", selectedSats: 100_000, inputCount: 1 }));
  const store = new LabStore({ admin, generateKey, buildSend, ...noopTimers() });
  return { admin, store, trackSpy, utxosSpy, broadcastSpy, generateKey, buildSend };
}

describe("LabStore", () => {
  // The lab wallet persists to localStorage; isolate each test.
  beforeEach(() => localStorage.clear());

  it("generate() derives a stable address and auto-tracks it", async () => {
    const { store, trackSpy, generateKey } = makeStore();

    await store.generate();

    expect(generateKey).toHaveBeenCalledOnce();
    expect(store.address).toBe(FAKE_KEY.address);
    // The WIF is held in memory only.
    expect(store.key?.wif).toBe(FAKE_KEY.wif);
    // Auto-track was called with ONLY the address (never the WIF).
    expect(trackSpy).toHaveBeenCalledWith({
      address: FAKE_KEY.address,
      name: "lab",
      historyMode: "forward_only",
    });
    const trackArg = JSON.stringify(trackSpy.mock.calls[0]?.[0]);
    expect(trackArg).not.toContain(FAKE_KEY.wif);
    expect(trackArg).not.toContain(FAKE_KEY._privHex);
  });

  it("generate() loads the UTXO set and computes balance", async () => {
    const { store } = makeStore();
    await store.generate();
    // The mock seeds one 100k-sat coin for any address.
    expect(store.utxos.length).toBe(1);
    expect(store.balanceSats).toBe(100_000);
  });

  it("send() builds a rawHex and broadcasts exactly that hex", async () => {
    const { store, buildSend, broadcastSpy } = makeStore();
    await store.generate();

    await store.send("1Destination000000000000000000000000", 50_000);

    expect(buildSend).toHaveBeenCalledOnce();
    const buildArgs = (buildSend as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(buildArgs.fromWif).toBe(FAKE_KEY.wif);
    expect(buildArgs.amountSats).toBe(50_000);
    // Broadcast received the exact rawHex the wrapper produced — and
    // nothing key-related.
    expect(broadcastSpy).toHaveBeenCalledWith("deadbeef");
    expect(store.receipt?.txId).toBeTruthy();
    expect(store.error).toBeNull();
  });

  it("send() surfaces insufficient-funds as a clear error", async () => {
    const buildSend = vi.fn(async () => {
      throw new Error("INSUFFICIENT_FUNDS");
    });
    const { store, broadcastSpy } = makeStore({ buildSend });
    await store.generate();

    await store.send("1Destination000000000000000000000000", 999_999_999);

    expect(store.error).toMatch(/insufficient funds/i);
    expect(store.receipt).toBeNull();
    expect(broadcastSpy).not.toHaveBeenCalled();
  });

  it("send() surfaces a broadcast 400 body via the admin client error", async () => {
    const admin = new MockAdminClient();
    vi.spyOn(admin, "broadcastRawTx").mockRejectedValue(new Error("16: bad-txns-inputs-missingorspent"));
    const store = new LabStore({
      admin,
      generateKey: async () => FAKE_KEY,
      buildSend: async () => ({ rawHex: "abcd", selectedSats: 100_000, inputCount: 1 }),
      ...noopTimers(),
    });
    await store.generate();

    await store.send("1Destination000000000000000000000000", 1_000);

    expect(store.error).toContain("bad-txns-inputs-missingorspent");
    expect(store.receipt).toBeNull();
  });

  it("send() refuses without a generated key", async () => {
    const { store, buildSend } = makeStore();
    await store.send("1Destination000000000000000000000000", 1_000);
    expect(buildSend).not.toHaveBeenCalled();
    expect(store.error).toMatch(/generate a key/i);
  });

  it("generate() persists the wallet so a new store restores it after refresh", async () => {
    const first = makeStore();
    await first.store.generate();
    expect(localStorage.getItem("consigliere.lab.wallet")).toContain(FAKE_KEY.address);

    // Simulate a page refresh: a brand-new store (no generate) hydrates
    // the keypair from localStorage and start() loads its UTXOs.
    const second = makeStore();
    expect(second.store.address).toBe(FAKE_KEY.address);
    expect(second.store.key?.wif).toBe(FAKE_KEY.wif);
    await second.store.start();
    expect(second.store.balanceSats).toBe(100_000);
    // The restored store can spend without regenerating.
    await second.store.send("1Destination000000000000000000000000", 10_000);
    expect(second.broadcastSpy).toHaveBeenCalledWith("deadbeef");
  });

  it("auto-polls the balance after a key is generated", async () => {
    let tick: (() => void) | null = null;
    const admin = new MockAdminClient();
    const utxosSpy = vi.spyOn(admin, "getAddressUtxos");
    const store = new LabStore({
      admin,
      generateKey: async () => FAKE_KEY,
      buildSend: async () => ({ rawHex: "x", selectedSats: 0, inputCount: 0 }),
      setInterval: (cb) => {
        tick = cb;
        return 1 as unknown as ReturnType<typeof setInterval>;
      },
      clearInterval: () => {},
    });

    await store.generate(); // starts polling + an initial refresh
    const afterGenerate = utxosSpy.mock.calls.length;
    expect(tick).not.toBeNull();

    tick!(); // simulate a poll tick → should refresh the balance again
    await Promise.resolve();
    expect(utxosSpy.mock.calls.length).toBeGreaterThan(afterGenerate);
  });

  it("reset() wipes the wallet from memory and localStorage", async () => {
    const { store } = makeStore();
    await store.generate();
    expect(localStorage.getItem("consigliere.lab.wallet")).not.toBeNull();

    store.reset();

    expect(store.key).toBeNull();
    expect(store.address).toBeNull();
    expect(store.utxos).toEqual([]);
    expect(localStorage.getItem("consigliere.lab.wallet")).toBeNull();
    // A fresh store no longer restores anything.
    expect(makeStore().store.address).toBeNull();
  });
});
