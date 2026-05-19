import { observer } from "mobx-react-lite";
import { Navigate, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import type { AuthStore } from "@/stores/root";
import { LOGIN_PATH } from "@/app/routes";

/**
 * Route guard. Bounces unauthenticated visitors to /login.
 *
 * Per Core Rule §13: every route except /login requires an
 * authenticated session. The API client's 401 handler bounces
 * server-driven session expiry; this guard handles the
 * already-loaded SPA where the user navigates while logged out.
 *
 * The `from` location is encoded in the redirect state so the
 * login form can return the operator to where they tried to go.
 */
export const AuthGuard = observer(function AuthGuard({
  auth,
  children,
}: {
  auth: AuthStore;
  children: ReactNode;
}) {
  const location = useLocation();
  if (!auth.isAuthenticated) {
    // wave-A2 S0: a fresh install reports setupRequired: true from
    // /api/admin/auth/me. Route the operator straight to the
    // public wizard instead of bouncing through /login first —
    // they have no credentials yet to sign in with.
    if (auth.setupRequired) {
      return <Navigate to="/setup" replace />;
    }
    // S2-audit M3: preserve the full URL (path + search + hash)
    // so post-login redirect returns the operator to the exact
    // page they tried to visit, including smart-search query
    // strings like `/headers?height=42`.
    const from = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate to={LOGIN_PATH} state={{ from }} replace />;
  }
  return <>{children}</>;
});
