import { makeAutoObservable, runInAction } from "mobx";
import { tokenApi, ApiResponseError } from "@/api/client";
import type { TrackedTokenListItem, TokenManageRequest } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error";

export class TokenListStore {
  items: TrackedTokenListItem[] = [];
  loadState: LoadState = "idle";
  error: string | null = null;
  includeTombstoned = false;

  private loadedKey: string | null = null;
  private inFlightKey: string | null = null;

  constructor() {
    makeAutoObservable(this);
  }

  private get hydrationKey() {
    return `tokens:${this.includeTombstoned}`;
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

  async add(body: TokenManageRequest): Promise<{ ok: boolean; error?: string }> {
    try {
      await tokenApi.manage(body);
      await this.reload();
      return { ok: true };
    } catch (err) {
      if (err instanceof ApiResponseError && err.status === 409) {
        return { ok: false, error: "Token already tracked." };
      }
      return { ok: false, error: "Failed to add token." };
    }
  }

  private async _load(key: string) {
    runInAction(() => {
      this.inFlightKey = key;
      this.loadState = "loading";
      this.error = null;
    });
    try {
      const items = await tokenApi.list(this.includeTombstoned);
      runInAction(() => {
        this.items = items;
        this.loadState = "success";
        this.loadedKey = key;
        this.inFlightKey = null;
      });
    } catch {
      runInAction(() => {
        this.loadState = "error";
        this.error = "Failed to load tracked tokens.";
        this.inFlightKey = null;
      });
    }
  }
}

export const tokenListStore = new TokenListStore();
