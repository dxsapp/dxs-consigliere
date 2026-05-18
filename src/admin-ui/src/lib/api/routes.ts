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
} as const;
