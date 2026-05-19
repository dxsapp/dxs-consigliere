import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  SetupCompleteRequest,
  SetupOptionsResponse,
  SetupStatusResponse,
} from "@/types/admin";

/**
 * wave-A2 S0 — first-run setup wizard store.
 *
 * Mirrors the wave-A1 detail-store pattern: abortable inflight,
 * no permanent `disposed` flag (S4-S6 audit fold), `start()`
 * idempotent under StrictMode.
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

export type SetupWizardStep = 1 | 2 | 3 | 4;

export interface SetupWizardAdminForm {
  username: string;
  password: string;
  confirmPassword: string;
}

export interface SetupWizardProvidersForm {
  rawTxPrimaryProvider: string;
  restFallbackProvider: string;
  realtimePrimaryProvider: string;
  bitailsTransport: string;
  bitailsApiKey: string;
  bitailsBaseUrl: string;
  bitailsWebsocketBaseUrl: string;
  bitailsZmqTxUrl: string;
  bitailsZmqBlockUrl: string;
  whatsonchainApiKey: string;
  whatsonchainBaseUrl: string;
  junglebusBaseUrl: string;
  junglebusMempoolSubscriptionId: string;
  nodeZmqTxUrl: string;
  nodeZmqBlockUrl: string;
}

export interface SetupWizardBlockSyncForm {
  baseUrl: string;
  blockSubscriptionId: string;
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
  providers: SetupWizardProvidersForm = blankProvidersForm();
  blockSync: SetupWizardBlockSyncForm = { baseUrl: "", blockSubscriptionId: "" };

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
        this.applyDefaults(opts);
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

  // ── Step navigation ─────────────────────────────────────────

  goNext(): void {
    if (this.errorsForStep(this.step).length > 0) return;
    if (this.step >= 4) return;
    this.step = (this.step + 1) as SetupWizardStep;
  }

  goBack(): void {
    if (this.step <= 1) return;
    this.step = (this.step - 1) as SetupWizardStep;
  }

  jumpTo(step: SetupWizardStep): void {
    // Backward jumps are always allowed. Forward jumps are allowed
    // only when every step we'd skip is clean — that way the
    // operator can land on the Review step via the Stepper rail
    // once the form is fully filled, but cannot bypass a step that
    // still has errors (e.g. password confirmation).
    if (step <= this.step) {
      this.step = step;
      return;
    }
    for (let s = this.step; s < step; s++) {
      if (this.errorsForStep(s as SetupWizardStep).length > 0) return;
    }
    this.step = step;
  }

  // ── Field setters ───────────────────────────────────────────

  setAdminField<K extends keyof SetupWizardAdminForm>(
    key: K,
    value: SetupWizardAdminForm[K]
  ): void {
    this.admin = { ...this.admin, [key]: value };
  }

  setProvidersField<K extends keyof SetupWizardProvidersForm>(
    key: K,
    value: SetupWizardProvidersForm[K]
  ): void {
    this.providers = { ...this.providers, [key]: value };
  }

  setBlockSyncField<K extends keyof SetupWizardBlockSyncForm>(
    key: K,
    value: SetupWizardBlockSyncForm[K]
  ): void {
    this.blockSync = { ...this.blockSync, [key]: value };
  }

  // ── Validation ──────────────────────────────────────────────

  errorsForStep(step: SetupWizardStep): FieldError[] {
    switch (step) {
      case 1:
        return this.adminErrors();
      case 2:
        return this.providersErrors();
      case 3:
        return this.blockSyncErrors();
      case 4:
        return [
          ...this.adminErrors(),
          ...this.providersErrors(),
          ...this.blockSyncErrors(),
        ];
      default:
        return [];
    }
  }

  get canSubmit(): boolean {
    return (
      this.status === "ready" &&
      this.step === 4 &&
      this.errorsForStep(4).length === 0
    );
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

  private applyDefaults(opts: SetupOptionsResponse): void {
    const p = opts.providerConfig;
    this.providers = {
      rawTxPrimaryProvider: opts.defaults.rawTxPrimaryProvider ?? "",
      restFallbackProvider: opts.defaults.restFallbackProvider ?? "",
      realtimePrimaryProvider: opts.defaults.realtimePrimaryProvider ?? "",
      bitailsTransport: opts.defaults.bitailsTransport ?? "",
      bitailsApiKey: p.bitails.apiKey ?? "",
      bitailsBaseUrl: p.bitails.baseUrl ?? "",
      bitailsWebsocketBaseUrl: p.bitails.websocketBaseUrl ?? "",
      bitailsZmqTxUrl: p.bitails.zmqTxUrl ?? "",
      bitailsZmqBlockUrl: p.bitails.zmqBlockUrl ?? "",
      whatsonchainApiKey: p.whatsonchain.apiKey ?? "",
      whatsonchainBaseUrl: p.whatsonchain.baseUrl ?? "",
      junglebusBaseUrl: p.junglebus.baseUrl ?? "",
      junglebusMempoolSubscriptionId: p.junglebus.mempoolSubscriptionId ?? "",
      nodeZmqTxUrl: p.node.zmqTxUrl ?? "",
      nodeZmqBlockUrl: p.node.zmqBlockUrl ?? "",
    };
    this.blockSync = {
      baseUrl: opts.blockSync.baseUrl ?? "",
      blockSubscriptionId: opts.blockSync.blockSubscriptionId ?? "",
    };
  }

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

  private providersErrors(): FieldError[] {
    const errors: FieldError[] = [];
    const allowed = this.options?.allowed;
    if (!allowed) {
      errors.push({ field: "providers", message: "Options not loaded yet" });
      return errors;
    }
    if (!allowed.rawTxPrimaryProviders.includes(this.providers.rawTxPrimaryProvider)) {
      errors.push({ field: "providers.rawTxPrimaryProvider", message: "Choose a primary rawTx provider" });
    }
    if (!allowed.restFallbackProviders.includes(this.providers.restFallbackProvider)) {
      errors.push({ field: "providers.restFallbackProvider", message: "Choose a REST fallback provider" });
    }
    if (!allowed.realtimePrimaryProviders.includes(this.providers.realtimePrimaryProvider)) {
      errors.push({ field: "providers.realtimePrimaryProvider", message: "Choose a realtime primary provider" });
    }
    if (!allowed.bitailsTransports.includes(this.providers.bitailsTransport)) {
      errors.push({ field: "providers.bitailsTransport", message: "Choose a Bitails transport" });
    }
    for (const [field, value] of [
      ["providers.bitailsBaseUrl", this.providers.bitailsBaseUrl],
      ["providers.bitailsWebsocketBaseUrl", this.providers.bitailsWebsocketBaseUrl],
      ["providers.whatsonchainBaseUrl", this.providers.whatsonchainBaseUrl],
      ["providers.junglebusBaseUrl", this.providers.junglebusBaseUrl],
    ] as const) {
      if (value.trim().length === 0) {
        errors.push({ field, message: "URL is required" });
        continue;
      }
      if (!isHttpUrl(value)) {
        errors.push({ field, message: "Must be an http(s) URL" });
      }
    }
    return errors;
  }

  private blockSyncErrors(): FieldError[] {
    const errors: FieldError[] = [];
    if (this.blockSync.baseUrl.trim().length === 0 || !isHttpUrl(this.blockSync.baseUrl)) {
      errors.push({ field: "blockSync.baseUrl", message: "Must be an http(s) URL" });
    }
    if (this.blockSync.blockSubscriptionId.trim().length === 0) {
      // Mirrors backend rule (SetupWizardService.cs:111).
      errors.push({ field: "blockSync.blockSubscriptionId", message: "JungleBus block subscription ID is required" });
    }
    return errors;
  }

  private buildRequest(): SetupCompleteRequest {
    return {
      admin: {
        enabled: true,
        username: this.admin.username.trim(),
        password: this.admin.password,
      },
      providers: {
        rawTxPrimaryProvider: this.providers.rawTxPrimaryProvider,
        restFallbackProvider: this.providers.restFallbackProvider,
        realtimePrimaryProvider: this.providers.realtimePrimaryProvider,
        bitailsTransport: this.providers.bitailsTransport,
        bitails: {
          apiKey: this.providers.bitailsApiKey,
          baseUrl: this.providers.bitailsBaseUrl,
          websocketBaseUrl: this.providers.bitailsWebsocketBaseUrl,
          zmqTxUrl: this.providers.bitailsZmqTxUrl,
          zmqBlockUrl: this.providers.bitailsZmqBlockUrl,
        },
        whatsonchain: {
          apiKey: this.providers.whatsonchainApiKey,
          baseUrl: this.providers.whatsonchainBaseUrl,
        },
        junglebus: {
          baseUrl: this.providers.junglebusBaseUrl,
          mempoolSubscriptionId: this.providers.junglebusMempoolSubscriptionId,
          blockSubscriptionId: this.blockSync.blockSubscriptionId.trim(),
        },
        node: {
          zmqTxUrl: this.providers.nodeZmqTxUrl,
          zmqBlockUrl: this.providers.nodeZmqBlockUrl,
        },
      },
      blockSync: {
        baseUrl: this.blockSync.baseUrl.trim(),
        blockSubscriptionId: this.blockSync.blockSubscriptionId.trim(),
      },
    };
  }
}

function blankProvidersForm(): SetupWizardProvidersForm {
  return {
    rawTxPrimaryProvider: "",
    restFallbackProvider: "",
    realtimePrimaryProvider: "",
    bitailsTransport: "",
    bitailsApiKey: "",
    bitailsBaseUrl: "",
    bitailsWebsocketBaseUrl: "",
    bitailsZmqTxUrl: "",
    bitailsZmqBlockUrl: "",
    whatsonchainApiKey: "",
    whatsonchainBaseUrl: "",
    junglebusBaseUrl: "",
    junglebusMempoolSubscriptionId: "",
    nodeZmqTxUrl: "",
    nodeZmqBlockUrl: "",
  };
}

function isHttpUrl(value: string): boolean {
  try {
    const u = new URL(value);
    return u.protocol === "http:" || u.protocol === "https:";
  } catch {
    return false;
  }
}

function errorMessage(err: unknown): string {
  if (err && typeof err === "object" && "message" in err) {
    const msg = (err as { message?: unknown }).message;
    if (typeof msg === "string" && msg.length > 0) return msg;
  }
  return "Setup failed";
}
