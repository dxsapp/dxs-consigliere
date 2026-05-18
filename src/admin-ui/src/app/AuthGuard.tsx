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
    return <Navigate to={LOGIN_PATH} state={{ from: location.pathname }} replace />;
  }
  return <>{children}</>;
});
