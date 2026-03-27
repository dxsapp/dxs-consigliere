import { makeAutoObservable, runInAction } from "mobx";
import { opsApi, dashboardApi, runtimeSourcesApi, ApiResponseError } from "@/api/client";
import { notifyStore } from "@/stores/notify.store";
import type { AdminRuntimeSources } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error";

export class OpsStore {
  providers: unknown = null;
  opsCache: unknown = null;
  opsStorage: unknown = null;
  adminCacheStatus: unknown = null;
  adminStorageStatus: unknown = null;
  runtimeSources: AdminRuntimeSources | null = null;
  realtimePrimarySourceDraft = "";
  bitailsTransportDraft = "";
  savingRealtimePolicy = false;
  resettingRealtimePolicy = false;

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

  get hasRealtimePolicyDraftChanges() {
    const baseline = this.runtimeSources?.realtimePolicy.override ?? this.runtimeSources?.realtimePolicy.effective;
    if (!baseline) return false;
    return (
      this.realtimePrimarySourceDraft !== baseline.primaryRealtimeSource ||
      this.bitailsTransportDraft !== baseline.bitailsTransport
    );
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
      const [providers, opsCache, opsStorage, adminCacheStatus, adminStorageStatus, runtimeSources] =
        await Promise.all([
          opsApi.providers().catch(() => null),
          opsApi.cache().catch(() => null),
          opsApi.storage().catch(() => null),
          dashboardApi.cacheStatus().catch(() => null),
          dashboardApi.storageStatus().catch(() => null),
          runtimeSourcesApi.get().catch(() => null),
        ]);
      runInAction(() => {
        this.providers = providers;
        this.opsCache = opsCache;
        this.opsStorage = opsStorage;
        this.adminCacheStatus = adminCacheStatus;
        this.adminStorageStatus = adminStorageStatus;
        this.runtimeSources = runtimeSources;
        this.syncRealtimePolicyDrafts();
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

  setRealtimePrimarySource(value: string) {
    this.realtimePrimarySourceDraft = value;
  }

  setBitailsTransport(value: string) {
    this.bitailsTransportDraft = value;
  }

  async applyRealtimePolicy() {
    if (!this.runtimeSources || this.savingRealtimePolicy) return;

    runInAction(() => {
      this.savingRealtimePolicy = true;
    });
    try {
      const snapshot = await runtimeSourcesApi.updateRealtimePolicy({
        primaryRealtimeSource: this.realtimePrimarySourceDraft,
        bitailsTransport: this.bitailsTransportDraft,
      });

      runInAction(() => {
        this.runtimeSources = snapshot;
        this.syncRealtimePolicyDrafts();
        this.savingRealtimePolicy = false;
      });
      notifyStore.success("Realtime policy override saved.");
    } catch (error) {
      runInAction(() => {
        this.savingRealtimePolicy = false;
      });
      if (error instanceof ApiResponseError) {
        notifyStore.error(`Failed to save realtime policy: ${error.code}.`);
      } else {
        notifyStore.error("Failed to save realtime policy.");
      }
    }
  }

  async resetRealtimePolicy() {
    if (!this.runtimeSources || this.resettingRealtimePolicy) return;

    runInAction(() => {
      this.resettingRealtimePolicy = true;
    });
    try {
      const snapshot = await runtimeSourcesApi.resetRealtimePolicy();

      runInAction(() => {
        this.runtimeSources = snapshot;
        this.syncRealtimePolicyDrafts();
        this.resettingRealtimePolicy = false;
      });
      notifyStore.success("Realtime policy override reset.");
    } catch (error) {
      runInAction(() => {
        this.resettingRealtimePolicy = false;
      });
      if (error instanceof ApiResponseError) {
        notifyStore.error(`Failed to reset realtime policy: ${error.code}.`);
      } else {
        notifyStore.error("Failed to reset realtime policy.");
      }
    }
  }

  private syncRealtimePolicyDrafts() {
    const baseline = this.runtimeSources?.realtimePolicy.override ?? this.runtimeSources?.realtimePolicy.effective;
    this.realtimePrimarySourceDraft = baseline?.primaryRealtimeSource ?? "";
    this.bitailsTransportDraft = baseline?.bitailsTransport ?? "";
  }
}

export const opsStore = new OpsStore();
