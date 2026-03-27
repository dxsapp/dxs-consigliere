import { makeAutoObservable, runInAction, computed } from "mobx";
import { findingsApi } from "@/api/client";
import type { Finding } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error";
export type SeverityFilter = "all" | "error" | "warning";

export class FindingsStore {
  items: Finding[] = [];
  loadState: LoadState = "idle";
  error: string | null = null;
  severityFilter: SeverityFilter = "all";
  take = 100;

  private loaded = false;
  private inFlight = false;

  constructor() {
    makeAutoObservable(this, {
      filteredItems: computed,
    });
  }

  get isLoading() {
    return this.loadState === "loading";
  }

  get filteredItems(): Finding[] {
    if (this.severityFilter === "all") return this.items;
    return this.items.filter((f) => f.severity === this.severityFilter);
  }

  get errorCount(): number {
    return this.items.filter((f) => f.severity === "error").length;
  }

  get warningCount(): number {
    return this.items.filter((f) => f.severity === "warning").length;
  }

  setSeverityFilter(filter: SeverityFilter) {
    this.severityFilter = filter;
  }

  async ensureLoaded() {
    if (this.loaded || this.inFlight) return;
    await this._load();
  }

  async reload() {
    if (this.inFlight) return;
    this.loaded = false;
    await this._load();
  }

  private async _load() {
    runInAction(() => {
      this.inFlight = true;
      this.loadState = "loading";
      this.error = null;
    });
    try {
      const items = await findingsApi.list(this.take);
      runInAction(() => {
        this.items = items;
        this.loadState = "success";
        this.loaded = true;
        this.inFlight = false;
      });
    } catch {
      runInAction(() => {
        this.loadState = "error";
        this.error = "Failed to load findings.";
        this.inFlight = false;
      });
    }
  }
}

export const findingsStore = new FindingsStore();
