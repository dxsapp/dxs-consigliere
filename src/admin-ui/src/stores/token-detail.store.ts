import { makeAutoObservable, runInAction } from "mobx";
import { tokenApi, ApiResponseError } from "@/api/client";
import type { TrackedTokenDetail } from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error" | "not_found";

export class TokenDetailStore {
  current: TrackedTokenDetail | null = null;
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

  async ensureLoaded(tokenId: string) {
    if (this.loadedId === tokenId || this.inFlightId === tokenId) return;
    await this._load(tokenId);
  }

  async reload() {
    if (!this.loadedId) return;
    const id = this.loadedId;
    this.loadedId = null;
    await this._load(id);
  }

  async untrack(tokenId: string): Promise<{ ok: boolean; error?: string }> {
    try {
      await tokenApi.untrack(tokenId);
      runInAction(() => {
        this.loadedId = null;
        this.current = null;
      });
      return { ok: true };
    } catch (err) {
      if (err instanceof ApiResponseError && err.status === 409) {
        runInAction(() => { this.managedByConfig = true; });
        return { ok: false, error: "This token is managed by config and cannot be untracked manually." };
      }
      return { ok: false, error: "Failed to untrack token." };
    }
  }

  async upgradeHistory(
    tokenId: string,
    trustedRoots: string[],
  ): Promise<{ ok: boolean; error?: string }> {
    try {
      await tokenApi.upgradeHistory(tokenId, { trustedRoots });
      this.loadedId = null;
      await this._load(tokenId);
      return { ok: true };
    } catch {
      return { ok: false, error: "Failed to upgrade history." };
    }
  }

  private async _load(tokenId: string) {
    runInAction(() => {
      this.inFlightId = tokenId;
      this.loadState = "loading";
      this.error = null;
      this.managedByConfig = false;
    });
    try {
      const detail = await tokenApi.detail(tokenId);
      runInAction(() => {
        this.current = detail;
        this.loadState = "success";
        this.loadedId = tokenId;
        this.inFlightId = null;
      });
    } catch (err) {
      runInAction(() => {
        this.inFlightId = null;
        if (err instanceof ApiResponseError && err.status === 404) {
          this.loadState = "not_found";
        } else {
          this.loadState = "error";
          this.error = "Failed to load token details.";
        }
      });
    }
  }
}

export const tokenDetailStore = new TokenDetailStore();
