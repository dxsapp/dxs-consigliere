import { makeAutoObservable, runInAction } from "mobx";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminAuditLogEntryResponse, AdminAuditLogResponse } from "@/types/admin";

/**
 * wave-A3 S3 — Audit log screen store. Read-only feed of the
 * destructive-ops record. Filter values are inputs the user
 * controls in the page; the fetch is debounced via a single
 * inflight `AbortController` so rapid filter edits don't pile
 * up requests.
 */
export interface AuditLogStoreOptions {
  admin: IAdminClient;
  /** Page size cap. Default 200. */
  lastN?: number;
}

const DEFAULT_LAST_N = 200;

export class AuditLogStore {
  status: "idle" | "loading" | "ready" | "error" = "idle";
  error: string | null = null;
  totalMatched = 0;
  entries: AdminAuditLogEntryResponse[] = [];

  filter: {
    action: string;
    username: string;
    since: string;
  } = { action: "", username: "", since: "" };

  private readonly admin: IAdminClient;
  private readonly lastN: number;
  private inflight: AbortController | null = null;

  constructor(opts: AuditLogStoreOptions) {
    this.admin = opts.admin;
    this.lastN = opts.lastN ?? DEFAULT_LAST_N;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  setActionFilter(value: string): void {
    this.filter = { ...this.filter, action: value };
  }

  setUsernameFilter(value: string): void {
    this.filter = { ...this.filter, username: value };
  }

  setSinceFilter(value: string): void {
    this.filter = { ...this.filter, since: value };
  }

  async refresh(): Promise<void> {
    if (this.inflight) this.inflight.abort();
    const controller = new AbortController();
    this.inflight = controller;
    this.status = "loading";
    this.error = null;
    try {
      const since = parseSince(this.filter.since);
      const response: AdminAuditLogResponse = await this.admin.getAuditLog({
        signal: controller.signal,
        lastN: this.lastN,
        action: this.filter.action || undefined,
        username: this.filter.username || undefined,
        since,
      });
      if (controller.signal.aborted) return;
      runInAction(() => {
        this.totalMatched = response.totalMatched;
        this.entries = response.entries;
        this.status = "ready";
      });
    } catch (err) {
      if (controller.signal.aborted) return;
      runInAction(() => {
        this.status = "error";
        this.error = describeError(err);
      });
    } finally {
      if (this.inflight === controller) this.inflight = null;
    }
  }

  dispose(): void {
    if (this.inflight) this.inflight.abort();
    this.inflight = null;
  }
}

function parseSince(raw: string): number | undefined {
  const trimmed = raw.trim();
  if (!trimmed) return undefined;
  // Accept either an ISO-8601 string or a unix-ms number.
  const asNumber = Number(trimmed);
  if (Number.isFinite(asNumber) && asNumber > 0) return asNumber;
  const parsed = Date.parse(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function describeError(err: unknown): string {
  if (typeof err === "object" && err && "message" in err) {
    const message = (err as { message?: string }).message;
    if (typeof message === "string" && message) return message;
  }
  return "Failed to load audit log";
}
