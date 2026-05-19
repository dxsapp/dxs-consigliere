import type { ApiClient } from "@/lib/api/client";
import {
  ADMIN_API_ROUTES,
  ADMIN_P2P_HEADERS_TIP_PATH,
  ADMIN_P2P_PEERS_PATH,
  ADMIN_PROVIDERS_PATH,
  SETUP_COMPLETE_PATH,
  SETUP_OPTIONS_PATH,
  SETUP_STATUS_PATH,
  adminAlertsPath,
  adminP2pHeadersRecentPath,
  adminTrackedAddressPath,
  adminTrackedTokenPath,
  TX_BROADCAST_PATH,
} from "@/lib/api/routes";
import type {
  AdminPeersResponse,
  AdminProvidersResponse,
  AdminTrackedAddressResponse,
  AdminTrackedTokenResponse,
  BroadcastReceiptDto,
  HeadersTipDto,
  P2pAlertResponse,
  P2pHealthDto,
  SetupCompleteRequest,
  SetupOptionsResponse,
  SetupStatusResponse,
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
  /** S7 — alert history (page-delta polling per A1 M1). */
  getAlerts(opts?: { lastN?: number; since?: number; signal?: AbortSignal }): Promise<P2pAlertResponse>;
  /** S8 — peers diagnostic. */
  getPeers(signal?: AbortSignal): Promise<AdminPeersResponse>;
  /** S9 — headers tip + recent. */
  getHeadersTip(signal?: AbortSignal): Promise<HeadersTipDto | null>;
  getHeadersRecent(count: number, signal?: AbortSignal): Promise<HeadersTipDto[]>;
  /** S10 — providers (config/recommendations/catalog). */
  getProviders(signal?: AbortSignal): Promise<AdminProvidersResponse>;
  /** S10 — setup wizard status. */
  getSetupStatus(signal?: AbortSignal): Promise<SetupStatusResponse>;
  /** wave-A2 S0 — setup wizard options (`AllowAnonymous`). */
  getSetupOptions(signal?: AbortSignal): Promise<SetupOptionsResponse>;
  /** wave-A2 S0 — submit the first-run wizard. */
  completeSetup(req: SetupCompleteRequest, signal?: AbortSignal): Promise<SetupStatusResponse>;
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

  getAlerts(opts: { lastN?: number; since?: number; signal?: AbortSignal } = {}) {
    return this.api.get<P2pAlertResponse>(
      adminAlertsPath({ lastN: opts.lastN, since: opts.since }),
      { signal: opts.signal }
    );
  }

  getPeers(signal?: AbortSignal) {
    return this.api.get<AdminPeersResponse>(ADMIN_P2P_PEERS_PATH, { signal });
  }

  async getHeadersTip(signal?: AbortSignal): Promise<HeadersTipDto | null> {
    try {
      return await this.api.get<HeadersTipDto>(ADMIN_P2P_HEADERS_TIP_PATH, { signal });
    } catch (err) {
      // The backend returns 404 before the chain bootstraps; that's
      // not an error — the operator just hasn't synced yet.
      const e = err as { status?: number; category?: string };
      if (e?.status === 404 || e?.category === "NotFound") return null;
      throw err;
    }
  }

  getHeadersRecent(count: number, signal?: AbortSignal) {
    return this.api.get<HeadersTipDto[]>(adminP2pHeadersRecentPath(count), { signal });
  }

  getProviders(signal?: AbortSignal) {
    return this.api.get<AdminProvidersResponse>(ADMIN_PROVIDERS_PATH, { signal });
  }

  getSetupStatus(signal?: AbortSignal) {
    return this.api.get<SetupStatusResponse>(SETUP_STATUS_PATH, { signal });
  }

  getSetupOptions(signal?: AbortSignal) {
    return this.api.get<SetupOptionsResponse>(SETUP_OPTIONS_PATH, { signal });
  }

  completeSetup(req: SetupCompleteRequest, signal?: AbortSignal) {
    return this.api.post<SetupStatusResponse>(SETUP_COMPLETE_PATH, req, { signal });
  }
}
