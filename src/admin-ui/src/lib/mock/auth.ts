import { makeAppError } from "@/types/errors";
import type {
  AdminAuthStatusResponse,
  AdminLoginRequest,
} from "@/types/auth";
import type { IAuthClient } from "@/lib/auth/client";

/**
 * S3 mock IAuthClient — mirrors the real backend's shape so the UI
 * works the same in `VITE_API_MODE=mock`.
 *
 * Seed credentials:
 *   username: "operator"
 *   password: "consigliere"
 * Anything else → 401-shaped AppError.
 */
export class MockAuthClient implements IAuthClient {
  private authenticated = false;
  private username = "";

  async me(): Promise<AdminAuthStatusResponse> {
    return this.statusResponse();
  }

  async login(req: AdminLoginRequest): Promise<AdminAuthStatusResponse> {
    if (!req.username.trim() || !req.password.trim()) {
      throw makeAppError("Validation", "credentials_required", 400);
    }
    if (req.username === "operator" && req.password === "consigliere") {
      this.authenticated = true;
      this.username = req.username.trim();
      return this.statusResponse();
    }
    throw makeAppError("Unauthorized", "invalid_credentials", 401);
  }

  async logout(): Promise<AdminAuthStatusResponse> {
    this.authenticated = false;
    this.username = "";
    return this.statusResponse();
  }

  private statusResponse(): AdminAuthStatusResponse {
    return {
      setupRequired: false,
      enabled: true,
      authenticated: this.authenticated,
      mode: "cookie",
      username: this.authenticated ? this.username : "",
      sessionTtlMinutes: 60,
    };
  }
}
