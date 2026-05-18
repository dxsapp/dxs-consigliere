import type { ApiClient } from "@/lib/api/client";
import { ADMIN_API_ROUTES } from "@/lib/api/routes";
import type {
  AdminAuthStatusResponse,
  AdminLoginRequest,
} from "@/types/auth";

/**
 * Admin auth API client. Wraps the three cookie-mode endpoints the
 * backend ships at `/api/admin/auth/*`:
 *
 *   GET  /me     → AdminAuthStatusResponse
 *   POST /login  → AdminAuthStatusResponse | 400 | 401 | 409
 *   POST /logout → AdminAuthStatusResponse
 *
 * The underlying ApiClient already sends `credentials: "include"`
 * so the auth cookie round-trips on every call.
 */
export interface IAuthClient {
  me(signal?: AbortSignal): Promise<AdminAuthStatusResponse>;
  login(req: AdminLoginRequest, signal?: AbortSignal): Promise<AdminAuthStatusResponse>;
  logout(signal?: AbortSignal): Promise<AdminAuthStatusResponse>;
}

export class AuthClient implements IAuthClient {
  constructor(private readonly api: ApiClient) {}

  me(signal?: AbortSignal) {
    return this.api.get<AdminAuthStatusResponse>(ADMIN_API_ROUTES.authMe, { signal });
  }

  login(req: AdminLoginRequest, signal?: AbortSignal) {
    return this.api.post<AdminAuthStatusResponse>(ADMIN_API_ROUTES.authLogin, req, { signal });
  }

  logout(signal?: AbortSignal) {
    return this.api.post<AdminAuthStatusResponse>(ADMIN_API_ROUTES.authLogout, {}, { signal });
  }
}
