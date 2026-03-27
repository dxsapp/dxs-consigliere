import { makeAutoObservable, runInAction } from "mobx";
import { addressApi, ApiResponseError } from "@/api/client";
import type { TrackedAddressDetail } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error" | "not_found";

export class AddressDetailStore {
  current: TrackedAddressDetail | null = null;
  loadState: LoadState = "idle";
  error: string | null = null;
  managedByConfig = false;

  private loadedId: string | null = null;
  private inFlightId: string | null = null;

  constructor() {
    makeAutoObservable(this);
  }

  get isLoading() {
    return this.loadState === "loading";
  }

  async ensureLoaded(address: string) {
    if (this.loadedId === address || this.inFlightId === address) return;
    await this._load(address);
  }

  async reload() {
    if (!this.loadedId) return;
    const id = this.loadedId;
    this.loadedId = null;
    await this._load(id);
  }

  async untrack(address: string): Promise<{ ok: boolean; error?: string }> {
    try {
      await addressApi.untrack(address);
      runInAction(() => {
        this.loadedId = null;
        this.current = null;
      });
      return { ok: true };
    } catch (err) {
      if (err instanceof ApiResponseError && err.status === 409) {
        runInAction(() => { this.managedByConfig = true; });
        return { ok: false, error: "This address is managed by config and cannot be untracked manually." };
      }
      return { ok: false, error: "Failed to untrack address." };
    }
  }

  async upgradeHistory(address: string): Promise<{ ok: boolean; error?: string }> {
    try {
      await addressApi.upgradeHistory(address);
      this.loadedId = null;
      await this._load(address);
      return { ok: true };
    } catch {
      return { ok: false, error: "Failed to upgrade history." };
    }
  }

  private async _load(address: string) {
    runInAction(() => {
      this.inFlightId = address;
      this.loadState = "loading";
      this.error = null;
      this.managedByConfig = false;
    });
    try {
      const detail = await addressApi.detail(address);
      runInAction(() => {
        this.current = detail;
        this.loadState = "success";
        this.loadedId = address;
        this.inFlightId = null;
      });
    } catch (err) {
      runInAction(() => {
        this.inFlightId = null;
        if (err instanceof ApiResponseError && err.status === 404) {
          this.loadState = "not_found";
        } else {
          this.loadState = "error";
          this.error = "Failed to load address details.";
        }
      });
    }
  }
}

export const addressDetailStore = new AddressDetailStore();
