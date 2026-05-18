import type { ApiClient } from "@/lib/api/client";
import { ADMIN_API_ROUTES } from "@/lib/api/routes";
import type {
  P2pHealthDto,
  SourceMetricsResponse,
} from "@/types/admin";

/**
 * S4 — REST client for the admin endpoints the Dashboard consumes:
 *   GET /api/admin/p2p/health
 *   GET /api/admin/metrics/sources?lastN=N
 *
 * One slice + one mock; future screen slices add their own methods
 * on the same interface.
 */
export interface IAdminClient {
  getP2pHealth(signal?: AbortSignal): Promise<P2pHealthDto>;
  getSourceMetrics(opts?: { lastN?: number; signal?: AbortSignal }): Promise<SourceMetricsResponse>;
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
}
