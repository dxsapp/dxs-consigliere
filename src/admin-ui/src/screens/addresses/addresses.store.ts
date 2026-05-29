import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type {
  AdminTrackAddressRequest,
  AdminTrackedAddressResponse,
} from "@/types/admin";

/**
 * Track-a-new — Addresses store. Mirrors the p2p/source-metrics store
 * idiom (constructor takes the admin client, load()/refresh(),
 * observable list + status). Adds a small form slice so an operator can
 * list currently-tracked addresses AND register a new one.
 *
 * History-mode judgment call: the form hardcodes `forward_only`. That is
 * the correct default for exercising the P2P mempool watchlist filter
 * (we only need to start watching from now on, not backfill history). A
 * `full_history` toggle is intentionally omitted to keep the surface
 * minimal; flip `HISTORY_MODE` if backfill becomes a requirement.
 */
const HISTORY_MODE = "forward_only";

export interface AddressesStoreOptions {
  admin: IAdminClient;
}

export class AddressesStore {
  addresses: AdminTrackedAddressResponse[] = [];
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;

  // ── add-a-new form slice ──────────────────────────────────────
  formAddress = "";
  formName = "";
  submitting = false;
  submitError: string | null = null;

  private readonly admin: IAdminClient;
  private inflight: AbortController | null = null;

  constructor(opts: AddressesStoreOptions) {
    this.admin = opts.admin;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async start(): Promise<void> {
    await this.refresh();
  }

  dispose(): void {
    this.inflight?.abort();
    this.inflight = null;
  }

  async refresh(): Promise<void> {
    this.inflight?.abort();
    const ctl = new AbortController();
    this.inflight = ctl;
    runInAction(() => {
      this.status = this.addresses.length ? "ready" : "loading";
    });
    try {
      const rows = await this.admin.getTrackedAddresses(false, ctl.signal);
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.addresses = rows;
        this.status = "ready";
        this.error = null;
      });
    } catch (err) {
      if (ctl.signal.aborted) return;
      runInAction(() => {
        this.status = "error";
        this.error = err instanceof Error ? err.message : "Unknown error";
      });
    } finally {
      if (this.inflight === ctl) this.inflight = null;
    }
  }

  setFormAddress(v: string): void {
    this.formAddress = v;
  }

  setFormName(v: string): void {
    this.formName = v;
  }

  get canSubmit(): boolean {
    return this.formAddress.trim().length > 0 && !this.submitting;
  }

  /** Submit the add form; on success refresh the list + clear inputs.
   *  Returns true on success so the page can react. */
  async submit(): Promise<boolean> {
    const address = this.formAddress.trim();
    if (!address || this.submitting) return false;
    runInAction(() => {
      this.submitting = true;
      this.submitError = null;
    });
    const req: AdminTrackAddressRequest = {
      address,
      name: this.formName.trim() || null,
      historyMode: HISTORY_MODE,
    };
    try {
      await this.admin.trackAddress(req);
      runInAction(() => {
        this.formAddress = "";
        this.formName = "";
        this.submitting = false;
      });
      await this.refresh();
      return true;
    } catch (err) {
      runInAction(() => {
        this.submitting = false;
        this.submitError = errorMessage(err);
      });
      return false;
    }
  }
}

/** Surface a readable error. The ApiClient maps a 400 into an
 *  `AppError` whose `message` is the raw response body — which for the
 *  track endpoints is `{"code":"..."}`. Pull the `code` out when the
 *  body is JSON so the operator sees `already_tracked` rather than a
 *  brace-wrapped blob; otherwise fall back to the message verbatim. */
export function errorMessage(err: unknown): string {
  const e = err as { message?: string } | undefined;
  const raw = e?.message;
  if (!raw) return "Unknown error";
  try {
    const parsed = JSON.parse(raw) as { code?: string };
    if (parsed && typeof parsed.code === "string" && parsed.code) return parsed.code;
  } catch {
    /* not JSON — use the message as-is */
  }
  return raw;
}
