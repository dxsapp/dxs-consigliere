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
 *
 * S7-S12-audit M1 followup: state persists in localStorage so the
 * e2e specs (which navigate via `page.goto` and therefore reload
 * the SPA on each step) keep their authenticated session, matching
 * the real cookie-mode behavior.
 */
const STORAGE_KEY = "consigliere-admin/mock-auth/v1";

interface PersistedState {
  authenticated: boolean;
  username: string;
}

function readPersisted(): PersistedState {
  if (typeof window === "undefined") return { authenticated: false, username: "" };
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return { authenticated: false, username: "" };
    const parsed = JSON.parse(raw);
    return {
      authenticated: parsed.authenticated === true,
      username: typeof parsed.username === "string" ? parsed.username : "",
    };
  } catch {
    return { authenticated: false, username: "" };
  }
}

function writePersisted(state: PersistedState): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    /* swallow — best-effort */
  }
}

export class MockAuthClient implements IAuthClient {
  private state: PersistedState = readPersisted();

  async me(): Promise<AdminAuthStatusResponse> {
    return this.statusResponse();
  }

  async login(req: AdminLoginRequest): Promise<AdminAuthStatusResponse> {
    if (!req.username.trim() || !req.password.trim()) {
      throw makeAppError("Validation", "credentials_required", 400);
    }
    if (req.username === "operator" && req.password === "consigliere") {
      this.state = { authenticated: true, username: req.username.trim() };
      writePersisted(this.state);
      return this.statusResponse();
    }
    throw makeAppError("Unauthorized", "invalid_credentials", 401);
  }

  async logout(): Promise<AdminAuthStatusResponse> {
    this.state = { authenticated: false, username: "" };
    writePersisted(this.state);
    return this.statusResponse();
  }

  private statusResponse(): AdminAuthStatusResponse {
    return {
      setupRequired: false,
      enabled: true,
      authenticated: this.state.authenticated,
      mode: "cookie",
      username: this.state.authenticated ? this.state.username : "",
      sessionTtlMinutes: 60,
    };
  }
}
