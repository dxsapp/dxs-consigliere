import { describe, expect, it } from "vitest";
import { ADMIN_API_ROUTES, createApiClients, resolveApiMode } from "./factory";
import { EventBus } from "@/lib/events/bus";
import { SignalRClient } from "@/lib/signalr/client";

describe("createApiClients (S3-audit H1)", () => {
  it("defaults to real mode when VITE_API_MODE is unset", () => {
    // resolveApiMode reads import.meta.env which Vitest mirrors;
    // in the test env we run without VITE_API_MODE set → real.
    expect(resolveApiMode()).toBe("real");
  });

  it("ADMIN_API_ROUTES.walletHubPath matches the backend WalletHub.Route constant", () => {
    // Backend: src/Dxs.Consigliere/WebSockets/WalletHub.cs
    //   public static string Route => "/ws/consigliere";
    // The S3-audit H1 fix anchors this constant; a backend rename
    // surfaces as a failing assertion here.
    expect(ADMIN_API_ROUTES.walletHubPath).toBe("/ws/consigliere");
  });

  it("real-mode SignalR client is configured with the canonical hub path", () => {
    const bus = new EventBus();
    const clients = createApiClients({ bus });
    expect(clients.mode).toBe("real");
    expect(clients.signalR).toBeInstanceOf(SignalRClient);
    expect((clients.signalR as SignalRClient).hubUrlForTests).toBe(
      ADMIN_API_ROUTES.walletHubPath
    );
  });

  it("admin auth route constants match the backend controller", () => {
    // Backend: src/Dxs.Consigliere/Controllers/AdminAuthController.cs
    //   [Route("api/admin/auth")] + me/login/logout
    expect(ADMIN_API_ROUTES.authMe).toBe("/api/admin/auth/me");
    expect(ADMIN_API_ROUTES.authLogin).toBe("/api/admin/auth/login");
    expect(ADMIN_API_ROUTES.authLogout).toBe("/api/admin/auth/logout");
  });
});
