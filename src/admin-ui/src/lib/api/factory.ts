import { ApiClient } from "@/lib/api/client";
import { ADMIN_API_ROUTES } from "@/lib/api/routes";
import { AuthClient, type IAuthClient } from "@/lib/auth/client";
import { MockAuthClient } from "@/lib/mock/auth";
import { MockSignalRClient } from "@/lib/mock/signalr";
import { SignalRClient, type ISignalRClient } from "@/lib/signalr/client";
import type { EventBus } from "@/lib/events/bus";

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
}

export interface ApiFactoryOptions {
  bus: EventBus;
  /** Default: empty (same-origin via Vite proxy in dev / ASP.NET
   *  static serve in production). */
  apiBase?: string;
  /** Default: `ADMIN_API_ROUTES.walletHubPath` (= `/ws/consigliere`). */
  hubUrl?: string;
}

export function createApiClients(opts: ApiFactoryOptions): ApiFactoryResult {
  const mode = resolveApiMode();
  const api = new ApiClient(opts.apiBase ?? "");
  if (mode === "mock") {
    return {
      mode,
      api,
      auth: new MockAuthClient(),
      signalR: new MockSignalRClient(opts.bus),
    };
  }
  return {
    mode,
    api,
    auth: new AuthClient(api),
    signalR: new SignalRClient(opts.bus, {
      hubUrl: opts.hubUrl ?? ADMIN_API_ROUTES.walletHubPath,
    }),
  };
}
