import { makeAutoObservable, runInAction } from "mobx";
import { ApiResponseError, setupApi } from "@/api/client";
import type { SetupCompleteRequest, SetupOptions, SetupStatus } from "@/types/api";
import { notifyStore } from "@/stores/notify.store";

export class SetupStore {
  options: SetupOptions | null = null;
  draft: SetupCompleteRequest | null = null;
  loading = false;
  saving = false;
  loaded = false;
  error: string | null = null;

  constructor() {
    makeAutoObservable(this);
  }

  get status(): SetupStatus | null {
    return this.options?.status ?? null;
  }

  async ensureLoaded() {
    if (this.loaded || this.loading) return;
    await this.load();
  }

  async reload() {
    if (this.loading) return;
    await this.load();
  }

  async complete(): Promise<SetupStatus | null> {
    if (!this.draft || this.saving) return null;

    runInAction(() => {
      this.saving = true;
      this.error = null;
    });

    try {
      const status = await setupApi.complete(this.draft);
      runInAction(() => {
        if (this.options) {
          this.options.status = status;
        }
        this.saving = false;
      });
      notifyStore.success("Setup saved.");
      return status;
    } catch (error) {
      runInAction(() => {
        this.saving = false;
        if (error instanceof ApiResponseError) {
          this.error = error.code;
        } else {
          this.error = "setup_failed";
        }
      });
      notifyStore.error(`Failed to save setup: ${this.error}.`);
      return null;
    }
  }

  setAdminEnabled(value: boolean) {
    if (!this.draft) return;
    this.draft.admin.enabled = value;
  }

  setAdminUsername(value: string) {
    if (!this.draft) return;
    this.draft.admin.username = value;
  }

  setAdminPassword(value: string) {
    if (!this.draft) return;
    this.draft.admin.password = value;
  }

  setRawTxProvider(value: string) {
    if (!this.draft) return;
    this.draft.providers.rawTxPrimaryProvider = value;
  }

  setRestFallbackProvider(value: string) {
    if (!this.draft) return;
    this.draft.providers.restFallbackProvider = value;
  }

  setRealtimeProvider(value: string) {
    if (!this.draft) return;
    this.draft.providers.realtimePrimaryProvider = value;
  }

  setBitailsTransport(value: string) {
    if (!this.draft) return;
    this.draft.providers.bitailsTransport = value;
  }

  setBitailsField(field: keyof SetupCompleteRequest["providers"]["bitails"], value: string) {
    if (!this.draft) return;
    this.draft.providers.bitails[field] = value;
  }

  setWhatsonchainField(field: keyof SetupCompleteRequest["providers"]["whatsonchain"], value: string) {
    if (!this.draft) return;
    this.draft.providers.whatsonchain[field] = value;
  }

  setJunglebusField(field: keyof SetupCompleteRequest["providers"]["junglebus"], value: string) {
    if (!this.draft) return;
    this.draft.providers.junglebus[field] = value;
  }

  setNodeField(field: keyof SetupCompleteRequest["providers"]["node"], value: string) {
    if (!this.draft) return;
    this.draft.providers.node[field] = value;
  }

  private async load() {
    runInAction(() => {
      this.loading = true;
      this.error = null;
    });

    try {
      const options = await setupApi.options();
      runInAction(() => {
        this.options = options;
        this.draft = {
          admin: {
            enabled: options.status.adminEnabled,
            username: options.status.adminUsername ?? "",
            password: "",
          },
          providers: {
            rawTxPrimaryProvider: options.defaults.rawTxPrimaryProvider,
            restFallbackProvider: options.defaults.restFallbackProvider,
            realtimePrimaryProvider: options.defaults.realtimePrimaryProvider,
            bitailsTransport: options.defaults.bitailsTransport,
            bitails: {
              apiKey: options.providerConfig.bitails.apiKey ?? "",
              baseUrl: options.providerConfig.bitails.baseUrl ?? "",
              websocketBaseUrl: options.providerConfig.bitails.websocketBaseUrl ?? "",
              zmqTxUrl: options.providerConfig.bitails.zmqTxUrl ?? "",
              zmqBlockUrl: options.providerConfig.bitails.zmqBlockUrl ?? "",
            },
            whatsonchain: {
              apiKey: options.providerConfig.whatsonchain.apiKey ?? "",
              baseUrl: options.providerConfig.whatsonchain.baseUrl ?? "",
            },
            junglebus: {
              baseUrl: options.providerConfig.junglebus.baseUrl ?? "",
              mempoolSubscriptionId: options.providerConfig.junglebus.mempoolSubscriptionId ?? "",
              blockSubscriptionId: options.providerConfig.junglebus.blockSubscriptionId ?? "",
            },
            node: {
              zmqTxUrl: options.providerConfig.node.zmqTxUrl ?? "",
              zmqBlockUrl: options.providerConfig.node.zmqBlockUrl ?? "",
            },
          },
        };
        this.loaded = true;
        this.loading = false;
      });
    } catch {
      runInAction(() => {
        this.error = "Failed to load setup options.";
        this.loading = false;
      });
    }
  }
}

export const setupStore = new SetupStore();
