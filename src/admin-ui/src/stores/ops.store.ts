import { makeAutoObservable, runInAction } from "mobx";
import { opsApi, dashboardApi } from "@/api/client";

type LoadState = "idle" | "loading" | "success" | "error";

export class OpsStore {
  providers: unknown = null;
  opsCache: unknown = null;
  opsStorage: unknown = null;
  adminCacheStatus: unknown = null;
  adminStorageStatus: unknown = null;

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
    await this._load(this.loaded);
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
      const [providers, opsCache, opsStorage, adminCacheStatus, adminStorageStatus] =
        await Promise.all([
          opsApi.providers().catch(() => null),
          opsApi.cache().catch(() => null),
          opsApi.storage().catch(() => null),
          dashboardApi.cacheStatus().catch(() => null),
          dashboardApi.storageStatus().catch(() => null),
        ]);
      runInAction(() => {
        this.providers = providers;
        this.opsCache = opsCache;
        this.opsStorage = opsStorage;
        this.adminCacheStatus = adminCacheStatus;
        this.adminStorageStatus = adminStorageStatus;
        this.loadState = "success";
        this.loaded = true;
        this.inFlight = false;
        this.refreshing = false;
      });
    } catch {
      runInAction(() => {
        this.loadState = "error";
        this.error = "Failed to load ops data.";
        this.inFlight = false;
        this.refreshing = false;
      });
    }
  }
}

export const opsStore = new OpsStore();
