import { makeAutoObservable, runInAction } from "mobx";
import { dashboardApi } from "@/api/client";
import type { DashboardSummary, ProjectionCacheStatusResponse, StorageStatusResponse, SyncStatusResponse } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error";

export class DashboardStore {
  summary: DashboardSummary | null = null;
  syncStatus: SyncStatusResponse | null = null;
  cacheStatus: ProjectionCacheStatusResponse | null = null;
  storageStatus: StorageStatusResponse | null = null;

  loadState: LoadState = "idle";
  refreshing = false;
  error: string | null = null;

  private loaded = false;
  private inFlight = false;

  constructor() {
    makeAutoObservable(this);
  }

  get isLoading() {
    return this.loadState === "loading";
  }

  async ensureLoaded() {
    if (this.loaded || this.inFlight) return;
    await this._load(false);
  }

  async reload() {
    if (this.inFlight) return;
    await this._load(this.loaded); // stale-refresh if already loaded
  }

  private async _load(staleRefresh: boolean) {
    runInAction(() => {
      this.inFlight = true;
      this.error = null;
      if (staleRefresh) {
        this.refreshing = true;
      } else {
        this.loadState = "loading";
      }
    });

    try {
      const [summary, syncStatus, cacheStatus, storageStatus] = await Promise.all([
        dashboardApi.summary(),
        dashboardApi.syncStatus().catch(() => null),
        dashboardApi.cacheStatus().catch(() => null),
        dashboardApi.storageStatus().catch(() => null),
      ]);
      runInAction(() => {
        this.summary = summary;
        this.syncStatus = syncStatus;
        this.cacheStatus = cacheStatus;
        this.storageStatus = storageStatus;
        this.loadState = "success";
        this.loaded = true;
        this.inFlight = false;
        this.refreshing = false;
      });
    } catch {
      runInAction(() => {
        this.loadState = "error";
        this.error = "Failed to load dashboard data.";
        this.inFlight = false;
        this.refreshing = false;
      });
    }
  }
}

export const dashboardStore = new DashboardStore();
