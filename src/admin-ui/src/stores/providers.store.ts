import { makeAutoObservable, runInAction } from "mobx";
import { ApiResponseError, providersApi } from "@/api/client";
import { notifyStore } from "@/stores/notify.store";
import type {
  AdminProviderConfigUpdateRequest,
  AdminProvidersResponse,
} from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error";

export class ProvidersStore {
  snapshot: AdminProvidersResponse | null = null;
  draft: AdminProviderConfigUpdateRequest | null = null;
  loadState: LoadState = "idle";
  refreshing = false;
  saving = false;
  resetting = false;
  error: string | null = null;

  private loaded = false;
  private inFlight = false;

  constructor() {
    makeAutoObservable(this);
  }

  get isLoading() {
    return this.loadState === "loading";
  }

  get config() {
    return this.snapshot?.config ?? null;
  }

  get hasDraftChanges() {
    if (!this.config || !this.draft) return false;
    return JSON.stringify(this.draft) !== JSON.stringify(this.config.effective);
  }

  async ensureLoaded() {
    if (this.loaded || this.inFlight) return;
    await this.load(false);
  }

  async reload() {
    if (this.inFlight) return;
    await this.load(this.loaded);
  }

  async apply() {
    if (!this.draft || this.saving) return;

    runInAction(() => {
      this.saving = true;
    });

    try {
      const snapshot = await providersApi.updateConfig(this.draft);
      runInAction(() => {
        this.snapshot = snapshot;
        this.syncDraft();
        this.saving = false;
      });
      notifyStore.success("Provider configuration saved.");
    } catch (error) {
      runInAction(() => {
        this.saving = false;
      });
      if (error instanceof ApiResponseError)
        notifyStore.error(`Failed to save provider configuration: ${error.code}.`);
      else
        notifyStore.error("Failed to save provider configuration.");
    }
  }

  async reset() {
    if (!this.config || this.resetting) return;

    runInAction(() => {
      this.resetting = true;
    });

    try {
      const snapshot = await providersApi.resetConfig();
      runInAction(() => {
        this.snapshot = snapshot;
        this.syncDraft();
        this.resetting = false;
      });
      notifyStore.success("Provider configuration reset.");
    } catch (error) {
      runInAction(() => {
        this.resetting = false;
      });
      if (error instanceof ApiResponseError)
        notifyStore.error(`Failed to reset provider configuration: ${error.code}.`);
      else
        notifyStore.error("Failed to reset provider configuration.");
    }
  }

  setRealtimePrimaryProvider(value: string) {
    if (!this.draft) return;
    this.draft.realtimePrimaryProvider = value;
  }

  setRestPrimaryProvider(value: string) {
    if (!this.draft) return;
    this.draft.restPrimaryProvider = value;
  }

  setBitailsTransport(value: string) {
    if (!this.draft) return;
    this.draft.bitailsTransport = value;
  }

  setBitailsField(field: keyof AdminProviderConfigUpdateRequest["bitails"], value: string) {
    if (!this.draft) return;
    this.draft.bitails[field] = value;
  }

  setWhatsonchainField(field: keyof AdminProviderConfigUpdateRequest["whatsonchain"], value: string) {
    if (!this.draft) return;
    this.draft.whatsonchain[field] = value;
  }

  setJunglebusField(field: keyof AdminProviderConfigUpdateRequest["junglebus"], value: string) {
    if (!this.draft) return;
    this.draft.junglebus[field] = value;
  }

  private async load(staleRefresh: boolean) {
    runInAction(() => {
      this.inFlight = true;
      this.error = null;
      if (staleRefresh) this.refreshing = true;
      else this.loadState = "loading";
    });

    try {
      const snapshot = await providersApi.get();
      runInAction(() => {
        this.snapshot = snapshot;
        this.syncDraft();
        this.loadState = "success";
        this.loaded = true;
        this.inFlight = false;
        this.refreshing = false;
      });
    } catch {
      runInAction(() => {
        this.loadState = "error";
        this.error = "Failed to load provider configuration.";
        this.inFlight = false;
        this.refreshing = false;
      });
    }
  }

  private syncDraft() {
    if (!this.snapshot) {
      this.draft = null;
      return;
    }
    const e = this.snapshot.config.effective;
    // Use explicit field mapping instead of structuredClone to guard against
    // backend returning null for nested provider objects.
    this.draft = {
      realtimePrimaryProvider: e.realtimePrimaryProvider ?? "",
      restPrimaryProvider: e.restPrimaryProvider ?? "",
      bitailsTransport: e.bitailsTransport ?? "",
      bitails: {
        apiKey: e.bitails?.apiKey ?? "",
        baseUrl: e.bitails?.baseUrl ?? "",
        websocketBaseUrl: e.bitails?.websocketBaseUrl ?? "",
        zmqTxUrl: e.bitails?.zmqTxUrl ?? "",
        zmqBlockUrl: e.bitails?.zmqBlockUrl ?? "",
      },
      whatsonchain: {
        apiKey: e.whatsonchain?.apiKey ?? "",
        baseUrl: e.whatsonchain?.baseUrl ?? "",
      },
      junglebus: {
        baseUrl: e.junglebus?.baseUrl ?? "",
        mempoolSubscriptionId: e.junglebus?.mempoolSubscriptionId ?? "",
        blockSubscriptionId: e.junglebus?.blockSubscriptionId ?? "",
      },
    };
  }
}

export const providersStore = new ProvidersStore();
