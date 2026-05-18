import type { ApiClient } from "@/lib/api/client";
import {
  ADMIN_API_ROUTES,
  adminTrackedAddressPath,
  adminTrackedTokenPath,
  TX_BROADCAST_PATH,
} from "@/lib/api/routes";
import type {
  AdminTrackedAddressResponse,
  AdminTrackedTokenResponse,
  BroadcastReceiptDto,
  P2pHealthDto,
  SourceMetricsResponse,
} from "@/types/admin";

/**
 * Admin REST client. Endpoints by slice:
 *   S4 — Dashboard:
 *     GET /api/admin/p2p/health
 *     GET /api/admin/metrics/sources?lastN=N
 *   S5 — Entity detail:
 *     GET /api/admin/tracked/address/{address}
 *     GET /api/admin/tracked/token/{tokenId}
 *
 * One slice + one mock; future screen slices add their own methods
 * on the same interface.
 */
export interface IAdminClient {
  getP2pHealth(signal?: AbortSignal): Promise<P2pHealthDto>;
  getSourceMetrics(opts?: { lastN?: number; signal?: AbortSignal }): Promise<SourceMetricsResponse>;
  getTrackedAddress(address: string, signal?: AbortSignal): Promise<AdminTrackedAddressResponse>;
  getTrackedToken(tokenId: string, signal?: AbortSignal): Promise<AdminTrackedTokenResponse>;
  /** S6 — submits a raw-hex tx via the canonical broadcast endpoint. */
  broadcastRaw(rawHex: string, signal?: AbortSignal): Promise<BroadcastReceiptDto>;
}

export class AdminClient implements IAdminClient {
  constructor(private readonly api: ApiClient) {}

  getP2pHealth(signal?: AbortSignal) {
    return this.api.get<P2pHealthDto>(ADMIN_API_ROUTES.p2pHealth, { signal });
  }

  getSourceMetrics(opts: { lastN?: number; signal?: AbortSignal } = {}) {
    const lastN = opts.lastN ?? 0;
    const path = lastN > 0
      ? `${ADMIN_API_ROUTES.metricsSources}?lastN=${lastN}`
      : ADMIN_API_ROUTES.metricsSources;
    return this.api.get<SourceMetricsResponse>(path, { signal: opts.signal });
  }

  getTrackedAddress(address: string, signal?: AbortSignal) {
    return this.api.get<AdminTrackedAddressResponse>(
      adminTrackedAddressPath(address),
      { signal }
    );
  }

  getTrackedToken(tokenId: string, signal?: AbortSignal) {
    return this.api.get<AdminTrackedTokenResponse>(
      adminTrackedTokenPath(tokenId),
      { signal }
    );
  }

  broadcastRaw(rawHex: string, signal?: AbortSignal) {
    return this.api.post<BroadcastReceiptDto>(
      TX_BROADCAST_PATH,
      { rawHex },
      { signal }
    );
  }
}
