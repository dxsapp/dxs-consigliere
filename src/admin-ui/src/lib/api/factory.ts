import { AdminClient, type IAdminClient } from "@/lib/admin/admin-client";
import { ApiClient } from "@/lib/api/client";
import { ADMIN_API_ROUTES } from "@/lib/api/routes";
import { AuthClient, type IAuthClient } from "@/lib/auth/client";
import { SignalRClient, type ISignalRClient } from "@/lib/signalr/client";
import type { EventBus } from "@/lib/events/bus";

// S6-audit M2: mock implementations are NEVER statically imported.
// Real-mode shell drops the ~5 KB of mock code via tree-shaking; in
// mock mode the factory dynamic-imports them on demand.

export { ADMIN_API_ROUTES } from "@/lib/api/routes";

/**
 * S3 wire factory. The `VITE_API_MODE` env switch picks the
 * implementation that backs the auth + SignalR clients:
 *
 *   VITE_API_MODE=real | <unset>  → ASP.NET backend through ApiClient
 *   VITE_API_MODE=mock           → in-process mocks (Core Rule §12)
 *
 * Mocks mirror the real DTO shape exactly. A future S3 followup
 * adds a contract-parity test that boots the real ASP.NET host and
 * validates DTOs from /api/admin/auth/me against the TS types.
 */
export type ApiMode = "real" | "mock";

export function resolveApiMode(): ApiMode {
  const v = import.meta.env.VITE_API_MODE;
  return v === "mock" ? "mock" : "real";
}

export interface ApiFactoryResult {
  mode: ApiMode;
  api: ApiClient;
  auth: IAuthClient;
  signalR: ISignalRClient;
  admin: IAdminClient;
}

export interface ApiFactoryOptions {
  bus: EventBus;
  /** Default: empty (same-origin via Vite proxy in dev / ASP.NET
   *  static serve in production). */
  apiBase?: string;
  /** Default: `ADMIN_API_ROUTES.walletHubPath` (= `/ws/consigliere`). */
  hubUrl?: string;
}

/**
 * Real-mode factory — synchronous, no mock imports. Used by tests
 * that inject their own clients and by `createApiClients` when the
 * env switch resolves to real.
 */
function buildRealClients(opts: ApiFactoryOptions): ApiFactoryResult {
  const api = new ApiClient(opts.apiBase ?? "");
  return {
    mode: "real",
    api,
    auth: new AuthClient(api),
    signalR: new SignalRClient(opts.bus, {
      hubUrl: opts.hubUrl ?? ADMIN_API_ROUTES.walletHubPath,
    }),
    admin: new AdminClient(api),
  };
}

/**
 * Async factory. Real mode returns synchronously-built clients
 * (wrapped in a resolved Promise); mock mode dynamic-imports the
 * mock implementations so they never land in the cold-load shell
 * (S6-audit M2).
 */
export async function createApiClients(opts: ApiFactoryOptions): Promise<ApiFactoryResult> {
  const mode = resolveApiMode();
  if (mode === "real") return buildRealClients(opts);
  const [{ MockAuthClient }, { MockSignalRClient }, { MockAdminClient }] = await Promise.all([
    import("@/lib/mock/auth"),
    import("@/lib/mock/signalr"),
    import("@/lib/mock/admin"),
  ]);
  return {
    mode: "mock",
    api: new ApiClient(opts.apiBase ?? ""),
    auth: new MockAuthClient(),
    signalR: new MockSignalRClient(opts.bus),
    admin: new MockAdminClient(),
  };
}
