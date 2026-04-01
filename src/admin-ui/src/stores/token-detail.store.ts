import { makeAutoObservable, runInAction } from "mobx";
import { tokenApi, ApiResponseError } from "@/api/client";
import type {
  GetUtxoSetResponse,
  TokenHistoryResponse,
  TokenStateResponse,
  TrackedTokenDetail,
} from "@/types/api";

type LoadState = "idle" | "loading" | "success" | "error" | "not_found";

export interface TokenOpsSummary {
  protocolType: string | null;
  protocolVersion: string | null;
  validationStatus: string | null;
  issuer: string | null;
  redeemAddress: string | null;
  totalKnownSupply: number | null;
  burnedSatoshis: number | null;
  lastIndexedHeight: number | null;
  holderCount: number | null;
  utxoCount: number | null;
  transactionCount: number | null;
  firstActivityAt: number | null;
  firstActivityHeight: number | null;
  lastActivityAt: number | null;
  lastActivityHeight: number | null;
  historyReadiness: string | null;
  trustedRootCount: number | null;
  completedTrustedRootCount: number | null;
  unknownRootFindingCount: number | null;
  rootedHistorySecure: boolean | null;
  blockingUnknownRoot: boolean | null;
}

export class TokenDetailStore {
  current: TrackedTokenDetail | null = null;
  summary: TokenOpsSummary | null = null;
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
      this.summaryLoadState = "loading";
      this.summaryError = null;
    });
    try {
      const detail = await tokenApi.detail(tokenId);
      runInAction(() => {
        this.current = detail;
        this.loadState = "success";
        this.loadedId = tokenId;
      });
      await this.loadSummary(tokenId);
    } catch (err) {
      runInAction(() => {
        if (err instanceof ApiResponseError && err.status === 404) {
          this.loadState = "not_found";
        } else {
          this.loadState = "error";
          this.error = "Failed to load token details.";
        }
        this.summaryLoadState = "error";
        this.summaryError = "Failed to load token summary.";
        this.inFlightId = null;
      });
    }
  }

  private async loadSummary(tokenId: string) {
    const [stateResult, balancesResult, utxosResult, historyAscResult, historyDescResult] = await Promise.allSettled([
      tokenApi.state(tokenId),
      tokenApi.balances(tokenId),
      tokenApi.utxos(tokenId),
      tokenApi.history(tokenId, { desc: false, take: 1, acceptPartialHistory: true }),
      tokenApi.history(tokenId, { desc: true, take: 1, acceptPartialHistory: true }),
    ]);

    const state = settledValue<TokenStateResponse>(stateResult);
    const balances = settledValue<{ address: string; tokenId: string | null; satoshis: number }[]>(balancesResult);
    const utxos = settledValue<GetUtxoSetResponse>(utxosResult);
    const historyAsc = settledValue<TokenHistoryResponse>(historyAscResult);
    const historyDesc = settledValue<TokenHistoryResponse>(historyDescResult);

    if (!state && !balances && !utxos && !historyAsc && !historyDesc) {
      runInAction(() => {
        this.summary = null;
        this.summaryLoadState = "error";
        this.summaryError = "Failed to load token summary.";
        this.inFlightId = null;
      });
      return;
    }

    const rootedToken = this.current?.readiness.history?.rootedToken;
    const effectiveBalances = balances ?? [];
    const effectiveUtxos = utxos?.utxoSet ?? [];
    const transactionCount = historyDesc?.totalCount ?? historyAsc?.totalCount ?? null;
    const firstHistoryItem = historyAsc?.history?.[0] ?? null;
    const lastHistoryItem = historyDesc?.history?.[0] ?? null;

    runInAction(() => {
      this.summary = {
        protocolType: state?.protocolType ?? (this.current?.protocolType as string | null) ?? null,
        protocolVersion: state?.protocolVersion ?? (this.current?.protocolVersion as string | null) ?? null,
        validationStatus: state?.validationStatus ?? (this.current?.validationStatus as string | null) ?? null,
        issuer: state?.issuer ?? (this.current?.issuer as string | null) ?? null,
        redeemAddress: state?.redeemAddress ?? (this.current?.redeemAddress as string | null) ?? null,
        totalKnownSupply: state?.totalKnownSupply ?? (this.current?.totalKnownSupply as number | null) ?? null,
        burnedSatoshis: state?.burnedSatoshis ?? (this.current?.burnedSatoshis as number | null) ?? null,
        lastIndexedHeight: state?.lastIndexedHeight ?? (this.current?.lastIndexedHeight as number | null) ?? null,
        holderCount: effectiveBalances.length,
        utxoCount: effectiveUtxos.length,
        transactionCount,
        firstActivityAt: firstHistoryItem?.timestamp ?? null,
        firstActivityHeight: firstHistoryItem?.height ?? null,
        lastActivityAt: lastHistoryItem?.timestamp ?? null,
        lastActivityHeight: lastHistoryItem?.height ?? null,
        historyReadiness:
          historyDesc?.historyStatus?.historyReadiness ??
          historyAsc?.historyStatus?.historyReadiness ??
          null,
        trustedRootCount: rootedToken?.trustedRootCount ?? null,
        completedTrustedRootCount: rootedToken?.completedTrustedRootCount ?? null,
        unknownRootFindingCount: rootedToken?.unknownRootFindingCount ?? null,
        rootedHistorySecure: rootedToken?.rootedHistorySecure ?? null,
        blockingUnknownRoot: rootedToken?.blockingUnknownRoot ?? null,
      };
      this.summaryLoadState = "success";
      this.inFlightId = null;
    });
  }
}

export const tokenDetailStore = new TokenDetailStore();

function settledValue<T>(result: PromiseSettledResult<T>): T | null {
  return result.status === "fulfilled" ? result.value : null;
}
