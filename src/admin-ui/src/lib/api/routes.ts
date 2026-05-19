/**
 * Backend route constants. Mirrored from C# sources:
 *  - `WalletHub.Route` at src/Dxs.Consigliere/WebSockets/WalletHub.cs
 *  - `AdminAuthController` at src/Dxs.Consigliere/Controllers/AdminAuthController.cs
 *
 * S3-audit H1 fix: the real SignalR client was previously defaulted
 * to `/wallethub`, which doesn't exist on the backend. Pinned here +
 * asserted in `factory.test.ts` so a backend rename surfaces as a
 * failing TS test, not a dead production connection.
 *
 * The S3 followup (contracts/swagger codegen) regenerates these
 * values from the live OpenAPI snapshot.
 */
export const ADMIN_API_ROUTES = {
  walletHubPath: "/ws/consigliere",
  authMe: "/api/admin/auth/me",
  authLogin: "/api/admin/auth/login",
  authLogout: "/api/admin/auth/logout",
  // S4 — admin REST surface consumed by the Dashboard.
  p2pHealth: "/api/admin/p2p/health",
  metricsSources: "/api/admin/metrics/sources",
} as const;

/** S5 — per-entity admin routes. Encoded path segments so a token
 *  id containing `/` or `:` round-trips correctly. */
export const adminTrackedAddressPath = (address: string) =>
  `/api/admin/tracked/address/${encodeURIComponent(address)}`;

export const adminTrackedTokenPath = (tokenId: string) =>
  `/api/admin/tracked/token/${encodeURIComponent(tokenId)}`;

/** S6 — canonical broadcast entrypoint (POST {rawHex} → BroadcastReceiptDto). */
export const TX_BROADCAST_PATH = "/api/tx/broadcast";

/** S7 — alert history (page-delta polling per A1 M1). Backend caps
 *  lastN ≤ min(retention, 1440); `since` is unix-ms exclusive. */
export const adminAlertsPath = (opts: { lastN?: number; since?: number } = {}): string => {
  const params: string[] = [];
  if (opts.lastN && opts.lastN > 0) params.push(`lastN=${opts.lastN}`);
  if (opts.since && opts.since > 0) params.push(`since=${opts.since}`);
  return params.length > 0 ? `/api/admin/p2p/alerts?${params.join("&")}` : "/api/admin/p2p/alerts";
};

/** S8 — peers + headers diagnostic surface. */
export const ADMIN_P2P_PEERS_PATH = "/api/admin/p2p/peers";
export const ADMIN_P2P_HEADERS_TIP_PATH = "/api/admin/p2p/headers/tip";
export const adminP2pHeadersRecentPath = (count: number) =>
  `/api/admin/p2p/headers/recent?count=${count}`;

/** S10 — providers config (read-only consumption in S10). */
export const ADMIN_PROVIDERS_PATH = "/api/admin/providers";

/** S10 — setup wizard status (`AllowAnonymous`). */
export const SETUP_STATUS_PATH = "/api/setup/status";

/** wave-A2 S0 — setup wizard options + completion (`AllowAnonymous`). */
export const SETUP_OPTIONS_PATH = "/api/setup/options";
export const SETUP_COMPLETE_PATH = "/api/setup/complete";
