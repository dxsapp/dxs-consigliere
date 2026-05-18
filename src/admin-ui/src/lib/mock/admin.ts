import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  AdminPeersResponse,
  AdminProvidersResponse,
  AdminTrackedAddressResponse,
  AdminTrackedTokenResponse,
  BroadcastReceiptDto,
  HeadersTipDto,
  P2pAlertResponse,
  P2pHealthDto,
  SourceMetricsResponse,
  SourceMetricsSnapshot,
} from "@/types/admin";
import { SOURCE_KEYS } from "@/types/admin";

/**
 * S4 mock IAdminClient. Mirrors the real backend response shapes
 * exactly (Core Rule §12). Returns a realistic seed:
 *  - 8 active peers in the pool
 *  - 24-snapshot history at ~30s cadence with growing FirstSeen
 *    counters per source
 *  - p2p source gets the highest rate by design
 */
export class MockAdminClient implements IAdminClient {
  /** Snapshot timestamps base; tests can override via constructor. */
  constructor(private readonly nowMs: () => number = () => Date.now()) {}

  async getP2pHealth(): Promise<P2pHealthDto> {
    return {
      bound: true,
      poolSize: 8,
      targetPoolSize: 8,
      subnet24Diversity: 6,
      activePeers: [
        "65.108.41.10:8333",
        "178.18.249.131:8333",
        "23.88.74.45:8333",
        "5.9.130.227:8333",
        "144.76.166.214:8333",
        "162.55.91.10:8333",
        "78.46.249.220:8333",
        "95.216.150.10:8333",
      ],
      inboundEnabled: false,
    };
  }

  async getTrackedAddress(address: string): Promise<AdminTrackedAddressResponse> {
    const { seedAddress } = await import("@/lib/mock/admin-tracked-seed");
    return seedAddress(address, this.nowMs());
  }

  async getTrackedToken(tokenId: string): Promise<AdminTrackedTokenResponse> {
    const { seedToken } = await import("@/lib/mock/admin-tracked-seed");
    return seedToken(tokenId, this.nowMs());
  }

  async getAlerts(opts: { lastN?: number; since?: number } = {}): Promise<P2pAlertResponse> {
    const { seedAlerts } = await import("@/lib/mock/admin-systems-seed");
    return seedAlerts(this.nowMs(), opts);
  }

  async getPeers(): Promise<AdminPeersResponse> {
    const { seedPeers } = await import("@/lib/mock/admin-systems-seed");
    return seedPeers(this.nowMs());
  }

  async getHeadersTip(): Promise<HeadersTipDto | null> {
    const { seedHeadersTip } = await import("@/lib/mock/admin-systems-seed");
    return seedHeadersTip(this.nowMs());
  }

  async getHeadersRecent(count: number): Promise<HeadersTipDto[]> {
    const { seedHeadersRecent } = await import("@/lib/mock/admin-systems-seed");
    return seedHeadersRecent(this.nowMs(), count);
  }

  async getProviders(): Promise<AdminProvidersResponse> {
    const { seedProviders } = await import("@/lib/mock/admin-systems-seed");
    return seedProviders();
  }

  async broadcastRaw(rawHex: string, _signal?: AbortSignal): Promise<BroadcastReceiptDto> {
    // Deterministic pseudo-txid: sha-like fold of rawHex; we only
    // need a stable 64-hex-char string for the UI confirmation.
    const txId = hexFold(rawHex);
    return {
      txId,
      state: "Validated",
      createdAtMs: this.nowMs(),
      failReason: null,
    };
  }

  async getSourceMetrics(opts: { lastN?: number } = {}): Promise<SourceMetricsResponse> {
    const lastN = Math.max(0, opts.lastN ?? 0);
    const history = lastN > 0 ? this.synthHistory(lastN) : [];
    const latest = this.snapshotAt(this.nowMs(), 0);
    return { latest, history };
  }

  private synthHistory(count: number): SourceMetricsSnapshot[] {
    const intervalMs = 30_000;
    const out: SourceMetricsSnapshot[] = [];
    const now = this.nowMs();
    // History is oldest-first per the C# response convention.
    for (let i = count - 1; i >= 0; i--) {
      out.push(this.snapshotAt(now - i * intervalMs, count - 1 - i));
    }
    return out;
  }

  private snapshotAt(snapshotUnixMs: number, ageIndex: number): SourceMetricsSnapshot {
    // Each source gets a stable growth curve; ageIndex 0 = newest.
    const ratePerSource = { p2p: 12, bitails: 11, junglebus: 4 } as const;
    const visibilityCounters = Object.fromEntries(
      SOURCE_KEYS.map((src) => [
        src,
        {
          firstSeen: 1_000 + ratePerSource[src] * ageIndex,
          onlySaw: Math.floor(ratePerSource[src] * 0.1 * ageIndex),
          lagBuckets: [50, 80, 60, 30, 12, 4],
        },
      ])
    );
    const observationCounters = Object.fromEntries(
      SOURCE_KEYS.map((src) => [
        src,
        {
          invObserved: 2_000 + ratePerSource[src] * ageIndex,
          matched: 1_900 + ratePerSource[src] * ageIndex,
          unmatched: 100 + Math.floor(ratePerSource[src] * 0.05 * ageIndex),
          parseError: 0,
          rateLimited: 0,
          getDataTimeout: 0,
          oversizePayload: 0,
        },
      ])
    );
    return {
      id: `metrics/sources/${snapshotUnixMs.toString().padStart(14, "0")}`,
      snapshotUnixMs,
      observationCounters,
      visibilityCounters,
      rebroadcast: {
        announced: 24,
        skippedNoRaw: 0,
        skippedCoinbase: 1,
        announceNoReadyPeer: 0,
        announceFailed: 0,
      },
      lastDegradedReorgAt: null,
    };
  }
}

/** Deterministic 64-hex-char fold of an arbitrary string. Suitable
 *  for mocked txIds where we just want a stable identifier per
 *  input. Not a real hash — never use for production semantics. */
function hexFold(input: string): string {
  let h1 = 0x811c9dc5;
  let h2 = 0xdeadbeef;
  for (let i = 0; i < input.length; i++) {
    const c = input.charCodeAt(i);
    h1 = Math.imul(h1 ^ c, 16777619) >>> 0;
    h2 = Math.imul(h2 ^ c, 2246822519) >>> 0;
  }
  const seed = h1.toString(16).padStart(8, "0") + h2.toString(16).padStart(8, "0");
  return seed.repeat(4).slice(0, 64);
}
