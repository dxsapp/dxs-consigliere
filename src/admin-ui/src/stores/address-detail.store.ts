import { makeAutoObservable, runInAction } from "mobx";
import { addressApi, ApiResponseError } from "@/api/client";
import type {
  AddressHistoryResponse,
  AddressStateResponse,
  TrackedAddressDetail,
} from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error" | "not_found";

export interface AddressOpsSummary {
  bsvBalanceSatoshis: number | null;
  trackedTokenBalanceCount: number | null;
  trackedTokenBalanceSatoshis: number | null;
  utxoCount: number | null;
  transactionCount: number | null;
  firstActivityAt: number | null;
  firstActivityHeight: number | null;
  lastActivityAt: number | null;
  lastActivityHeight: number | null;
  historyReadiness: string | null;
}

export class AddressDetailStore {
  current: TrackedAddressDetail | null = null;
  summary: AddressOpsSummary | null = null;
  loadState: LoadState = "idle";
  summaryLoadState: LoadState = "idle";
  error: string | null = null;
  summaryError: string | null = null;
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
      this.summaryLoadState = "loading";
      this.summaryError = null;
    });
    try {
      const detail = await addressApi.detail(address);
      runInAction(() => {
        this.current = detail;
        this.loadState = "success";
        this.loadedId = address;
      });
      await this.loadSummary(address);
    } catch (err) {
      runInAction(() => {
        if (err instanceof ApiResponseError && err.status === 404) {
          this.loadState = "not_found";
        } else {
          this.loadState = "error";
          this.error = "Failed to load address details.";
        }
        this.summaryLoadState = "error";
        this.summaryError = "Failed to load address summary.";
        this.inFlightId = null;
      });
    }
  }

  private async loadSummary(address: string) {
    const [stateResult, historyAscResult, historyDescResult] = await Promise.allSettled([
      addressApi.state(address),
      addressApi.history(address, { desc: false, take: 1, acceptPartialHistory: true }),
      addressApi.history(address, { desc: true, take: 1, acceptPartialHistory: true }),
    ]);

    const state = settledValue<AddressStateResponse>(stateResult);
    const historyAsc = settledValue<AddressHistoryResponse>(historyAscResult);
    const historyDesc = settledValue<AddressHistoryResponse>(historyDescResult);

    if (!state && !historyAsc && !historyDesc) {
      runInAction(() => {
        this.summary = null;
        this.summaryLoadState = "error";
        this.summaryError = "Failed to load address summary.";
        this.inFlightId = null;
      });
      return;
    }

    const balances = state?.balances ?? [];
    const bsvBalanceSatoshis = balances
      .filter((balance) => !balance.tokenId)
      .reduce((sum, balance) => sum + balance.satoshis, 0);
    const tokenBalanceEntries = balances.filter((balance) => Boolean(balance.tokenId));
    const tokenBalanceSatoshis = tokenBalanceEntries.reduce((sum, balance) => sum + balance.satoshis, 0);
    const utxoCount = state?.utxoSet?.length ?? null;
    const transactionCount = historyDesc?.totalCount ?? historyAsc?.totalCount ?? null;
    const firstHistoryItem = historyAsc?.history?.[0] ?? null;
    const lastHistoryItem = historyDesc?.history?.[0] ?? null;

    runInAction(() => {
      this.summary = {
        bsvBalanceSatoshis,
        trackedTokenBalanceCount: tokenBalanceEntries.length,
        trackedTokenBalanceSatoshis: tokenBalanceSatoshis,
        utxoCount,
        transactionCount,
        firstActivityAt: firstHistoryItem?.timestamp ?? null,
        firstActivityHeight: firstHistoryItem?.height ?? null,
        lastActivityAt: lastHistoryItem?.timestamp ?? null,
        lastActivityHeight: lastHistoryItem?.height ?? null,
        historyReadiness:
          historyDesc?.historyStatus?.historyReadiness ??
          historyAsc?.historyStatus?.historyReadiness ??
          null,
      };
      this.summaryLoadState = "success";
      this.inFlightId = null;
    });
  }
}

export const addressDetailStore = new AddressDetailStore();

function settledValue<T>(result: PromiseSettledResult<T>): T | null {
  return result.status === "fulfilled" ? result.value : null;
}
