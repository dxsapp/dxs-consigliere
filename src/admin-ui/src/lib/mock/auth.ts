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

// wave-A2 S0 — wizard hands off { username, password } via a
// dedicated key so the auth mock can accept them on login.
const WIZARD_CREDENTIALS_KEY = "consigliere-admin/mock-auth-credentials/v1";

function readWizardCredentials(): { username: string; password: string } | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(WIZARD_CREDENTIALS_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (typeof parsed.username !== "string" || typeof parsed.password !== "string") {
      return null;
    }
    return { username: parsed.username, password: parsed.password };
  } catch {
    return null;
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
    // wave-A2 S0 — when the mock setup wizard ran, it stashed the
    // chosen `{username, password}` in localStorage. Accept those
    // first; otherwise fall back to the seeded operator/consigliere
    // pair so existing e2e specs + unit tests stay green without
    // walking through the wizard.
    const wizardCreds = readWizardCredentials();
    if (
      wizardCreds &&
      req.username === wizardCreds.username &&
      req.password === wizardCreds.password
    ) {
      this.state = { authenticated: true, username: wizardCreds.username };
      writePersisted(this.state);
      return this.statusResponse();
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
    // wave-A2 S0: reflect the mock setup state so the LoginPage
    // banner fires (and the AuthGuard / SetupWizardPage route the
    // operator correctly) before the real install has been done.
    const setupCompleted = readMockSetupCompleted();
    return {
      setupRequired: !setupCompleted,
      enabled: true,
      authenticated: this.state.authenticated,
      mode: "cookie",
      username: this.state.authenticated ? this.state.username : "",
      sessionTtlMinutes: 60,
    };
  }
}

// wave-A2 S0 — peek at the mock setup state key written by the
// SetupWizardStore via MockAdminClient.completeSetup.
const SETUP_STATE_KEY = "consigliere-admin/mock-setup-state/v1";

function readMockSetupCompleted(): boolean {
  if (typeof window === "undefined") return true; // SSR / Node-side fallback — never trip the banner in tests that don't care.
  try {
    const raw = window.localStorage.getItem(SETUP_STATE_KEY);
    if (!raw) return false;
    const parsed = JSON.parse(raw);
    return parsed.setupCompleted === true;
  } catch {
    return false;
  }
}
