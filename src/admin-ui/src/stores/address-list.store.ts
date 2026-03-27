import { makeAutoObservable, runInAction } from "mobx";
import { addressApi, ApiResponseError } from "@/api/client";
import type { TrackedAddressListItem, AddressManageRequest } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error";

export class AddressListStore {
  items: TrackedAddressListItem[] = [];
  loadState: LoadState = "idle";
  error: string | null = null;
  includeTombstoned = false;

  private loadedKey: string | null = null;
  private inFlightKey: string | null = null;

  constructor() {
    makeAutoObservable(this);
  }

  private get hydrationKey() {
    return `addresses:${this.includeTombstoned}`;
  }

  get isLoading() {
    return this.loadState === "loading";
  }

  async ensureLoaded() {
    const key = this.hydrationKey;
    if (this.loadedKey === key || this.inFlightKey === key) return;
    await this._load(key);
  }

  async reload() {
    this.loadedKey = null;
    await this._load(this.hydrationKey);
  }

  setIncludeTombstoned(value: boolean) {
    this.includeTombstoned = value;
    this.loadedKey = null;
  }

  invalidate() {
    this.loadedKey = null;
  }

  async add(body: AddressManageRequest): Promise<{ ok: boolean; error?: string }> {
    try {
      await addressApi.manage(body);
      await this.reload();
      return { ok: true };
    } catch (err) {
      if (err instanceof ApiResponseError && err.status === 409) {
        return { ok: false, error: "Address already tracked." };
      }
      return { ok: false, error: "Failed to add address." };
    }
  }

  private async _load(key: string) {
    runInAction(() => {
      this.inFlightKey = key;
      this.loadState = "loading";
      this.error = null;
    });
    try {
      const items = await addressApi.list(this.includeTombstoned);
      runInAction(() => {
        this.items = items;
        this.loadState = "success";
        this.loadedKey = key;
        this.inFlightKey = null;
      });
    } catch {
      runInAction(() => {
        this.loadState = "error";
        this.error = "Failed to load tracked addresses.";
        this.inFlightKey = null;
      });
    }
  }
}

export const addressListStore = new AddressListStore();
