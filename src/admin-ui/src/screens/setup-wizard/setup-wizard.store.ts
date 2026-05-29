import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  SetupCompleteRequest,
  SetupOptionsResponse,
  SetupStatusResponse,
} from "@/types/admin";

/**
 * wave simplified-first-run-wizard S2 — first-run setup wizard store.
 *
 * Collapsed from the original four-step flow (Admin → Providers →
 * Block sync → Review) to a SINGLE required step: create the admin
 * account. The thin node runs on the built-in P2P source, so no
 * external providers and no JungleBus block subscription are needed
 * to reach a working product. Providers + history sync remain
 * configurable later via the Settings screens.
 *
 * `buildRequest` therefore sends the `admin` object only. The
 * generated `SetupCompleteRequest` still types `providers`/`blockSync`
 * as present (contract shape unchanged — wave-A4 S3: types stay
 * generated re-exports), so we send them as EMPTY objects. The
 * backend (S1) treats an empty providers selection + empty block sync
 * as "absent" → keeps the seeded p2p-primary defaults, skips the
 * provider-config apply, and never requires a subscription.
 *
 * State shape:
 *   loading   — initial fetch of /api/setup/options in flight
 *   ready     — options loaded, operator is editing the form
 *   submitting — POST /api/setup/complete in flight
 *   submitted — backend returned success; page redirects to /login
 *   error     — initial fetch failed; recoverable via retry
 */
export type SetupWizardStatus =
  | "loading"
  | "ready"
  | "submitting"
  | "submitted"
  | "error";

/**
 * Single-step first-run flow. The type is retained (rather than
 * inlined) so the page's `data-testid="setup-step-1"` and any future
 * re-expansion stay explicit.
 */
export type SetupWizardStep = 1;

export interface SetupWizardAdminForm {
  username: string;
  password: string;
  confirmPassword: string;
}

export interface SetupWizardOptions {
  admin: IAdminClient;
}

export interface FieldError {
  field: string;
  message: string;
}

export class SetupWizardStore {
  status: SetupWizardStatus = "loading";
  step: SetupWizardStep = 1;
  options: SetupOptionsResponse | null = null;
  error: string | null = null;
  submittedStatus: SetupStatusResponse | null = null;

  admin: SetupWizardAdminForm = {
    username: "",
    password: "",
    confirmPassword: "",
  };

  private readonly client: IAdminClient;
  private inflight: AbortController | null = null;

  constructor(opts: SetupWizardOptions) {
    this.client = opts.admin;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  // ── Lifecycle ────────────────────────────────────────────────

  async start(): Promise<void> {
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.options ? "ready" : "loading";
      this.error = null;
    });
    try {
      const opts = await this.client.getSetupOptions(ctl.signal);
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.options = opts;
        this.status = "ready";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.status = "error";
        this.error = err instanceof Error ? err.message : "Failed to load setup options";
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  dispose(): void {
    this.inflight?.abort();
    this.inflight = null;
  }

  // ── Field setters ───────────────────────────────────────────

  setAdminField<K extends keyof SetupWizardAdminForm>(
    key: K,
    value: SetupWizardAdminForm[K]
  ): void {
    this.admin = { ...this.admin, [key]: value };
  }

  // ── Validation ──────────────────────────────────────────────

  errorsForStep(step: SetupWizardStep): FieldError[] {
    switch (step) {
      case 1:
        return this.adminErrors();
      default:
        return [];
    }
  }

  /**
   * Ready to complete once the admin account fields validate. There is
   * no provider/block-sync gating in the first-run flow any more.
   */
  get canSubmit(): boolean {
    return this.status === "ready" && this.adminErrors().length === 0;
  }

  // ── Submit ──────────────────────────────────────────────────

  async submit(): Promise<void> {
    if (!this.canSubmit) return;
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = "submitting";
      this.error = null;
    });
    try {
      const req = this.buildRequest();
      const res = await this.client.completeSetup(req, ctl.signal);
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.submittedStatus = res;
        this.status = "submitted";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.status = "ready";
        this.error = errorMessage(err);
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  // ── Internals ───────────────────────────────────────────────

  private adminErrors(): FieldError[] {
    const errors: FieldError[] = [];
    if (this.admin.username.trim().length < 3) {
      errors.push({ field: "admin.username", message: "Username must be at least 3 characters" });
    }
    if (this.admin.password.length < 8) {
      errors.push({ field: "admin.password", message: "Password must be at least 8 characters" });
    }
    if (this.admin.password !== this.admin.confirmPassword) {
      errors.push({ field: "admin.confirmPassword", message: "Passwords do not match" });
    }
    return errors;
  }

  /**
   * Admin-only complete request. `providers`/`blockSync` are sent as
   * empty objects: the backend (S1) reads an empty provider selection +
   * empty block sync as "absent" and keeps the seeded p2p-primary
   * defaults — no third-party provider or subscription required. The
   * fields stay present only because the generated DTO types them as
   * required (contract shape unchanged).
   */
  private buildRequest(): SetupCompleteRequest {
    return {
      admin: {
        enabled: true,
        username: this.admin.username.trim(),
        password: this.admin.password,
      },
      providers: {
        rawTxPrimaryProvider: "",
        restFallbackProvider: "",
        realtimePrimaryProvider: "",
        bitailsTransport: "",
        bitails: {
          apiKey: "",
          baseUrl: "",
          websocketBaseUrl: "",
          zmqTxUrl: "",
          zmqBlockUrl: "",
        },
        whatsonchain: {
          apiKey: "",
          baseUrl: "",
        },
        junglebus: {
          baseUrl: "",
          mempoolSubscriptionId: "",
          blockSubscriptionId: "",
        },
        node: {
          zmqTxUrl: "",
          zmqBlockUrl: "",
        },
      },
      blockSync: {
        baseUrl: "",
        blockSubscriptionId: "",
      },
    };
  }
}

function errorMessage(err: unknown): string {
  if (err && typeof err === "object" && "message" in err) {
    const msg = (err as { message?: unknown }).message;
    if (typeof msg === "string" && msg.length > 0) return msg;
  }
  return "Setup failed";
}
