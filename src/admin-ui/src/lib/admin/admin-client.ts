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
  adminAuditLogPath,
  adminP2pHeadersRecentPath,
  adminTrackedAddressPath,
  adminTrackedAddressesPath,
  adminTrackedTokenPath,
  adminTrackedTokensPath,
  addressUtxosPath,
  txExternalSightingPath,
  TX_BROADCAST_PATH,
} from "@/lib/api/routes";
import type {
  AdminAuditLogResponse,
  AdminPeersResponse,
  AdminProvidersResponse,
  AdminTrackAddressRequest,
  AdminTrackTokenRequest,
  AdminTrackedAddressResponse,
  AdminTrackedTokenResponse,
  BroadcastReceiptDto,
  ExternalSightingResponse,
  GetUtxoSetResponse,
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
  /** Track-a-new — list + add tracked addresses / tokens. */
  getTrackedAddresses(
    includeTombstoned?: boolean,
    signal?: AbortSignal
  ): Promise<AdminTrackedAddressResponse[]>;
  getTrackedTokens(
    includeTombstoned?: boolean,
    signal?: AbortSignal
  ): Promise<AdminTrackedTokenResponse[]>;
  trackAddress(
    req: AdminTrackAddressRequest,
    signal?: AbortSignal
  ): Promise<AdminTrackedAddressResponse>;
  trackToken(
    req: AdminTrackTokenRequest,
    signal?: AbortSignal
  ): Promise<AdminTrackedTokenResponse>;
  /** S6 — submits a raw-hex tx via the canonical broadcast endpoint. */
  broadcastRaw(rawHex: string, signal?: AbortSignal): Promise<BroadcastReceiptDto>;
  /** tx-lab S1 — same-origin UTXO lookup for the lab screen. */
  getAddressUtxos(address: string, signal?: AbortSignal): Promise<GetUtxoSetResponse>;
  /** Broadcast inspector — does the named public explorer see this txid yet?
   *  source ∈ { woc, bitails, junglebus }. */
  getExternalSighting(
    txId: string,
    source: string,
    signal?: AbortSignal
  ): Promise<ExternalSightingResponse>;
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
  /** wave-A3 S3 — read-only audit log feed. */
  getAuditLog(opts?: {
    since?: number;
    action?: string;
    username?: string;
    lastN?: number;
    signal?: AbortSignal;
  }): Promise<AdminAuditLogResponse>;
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

  getTrackedAddresses(includeTombstoned = false, signal?: AbortSignal) {
    return this.api.get<AdminTrackedAddressResponse[]>(
      adminTrackedAddressesPath(includeTombstoned),
      { signal }
    );
  }

  getTrackedTokens(includeTombstoned = false, signal?: AbortSignal) {
    return this.api.get<AdminTrackedTokenResponse[]>(
      adminTrackedTokensPath(includeTombstoned),
      { signal }
    );
  }

  trackAddress(req: AdminTrackAddressRequest, signal?: AbortSignal) {
    return this.api.post<AdminTrackedAddressResponse>(
      adminTrackedAddressesPath(),
      req,
      { signal }
    );
  }

  trackToken(req: AdminTrackTokenRequest, signal?: AbortSignal) {
    return this.api.post<AdminTrackedTokenResponse>(
      adminTrackedTokensPath(),
      req,
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

  getAddressUtxos(address: string, signal?: AbortSignal) {
    return this.api.get<GetUtxoSetResponse>(addressUtxosPath(address), { signal });
  }

  getExternalSighting(txId: string, source: string, signal?: AbortSignal) {
    return this.api.get<ExternalSightingResponse>(
      txExternalSightingPath(txId, source),
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

  getAuditLog(opts: {
    since?: number;
    action?: string;
    username?: string;
    lastN?: number;
    signal?: AbortSignal;
  } = {}) {
    return this.api.get<AdminAuditLogResponse>(
      adminAuditLogPath({
        since: opts.since,
        action: opts.action,
        username: opts.username,
        lastN: opts.lastN,
      }),
      { signal: opts.signal },
    );
  }
}
