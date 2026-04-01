import type {
  AuthStatus,
  LoginRequest,
  TrackedAddressListItem,
  TrackedAddressDetail,
  TrackedTokenListItem,
  TrackedTokenDetail,
  UntrackResult,
  Finding,
  DashboardSummary,
  AdminProvidersResponse,
  AdminProviderConfigUpdateRequest,
  SetupStatus,
  SetupOptions,
  SetupCompleteRequest,
  AddressManageRequest,
  TokenManageRequest,
  TokenHistoryUpgradeRequest,
  AddressStateResponse,
  AddressHistoryResponse,
  BalanceDto,
  GetUtxoSetResponse,
  TokenStateResponse,
  TokenHistoryResponse,
  SyncStatusResponse,
  ProviderStatusResponse,
  JungleBusBlockSyncStatusResponse,
  JungleBusChainTipAssuranceResponse,
} from "@/types/api";

// ─── Error model ─────────────────────────────────────────────────────────────

export class ApiResponseError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    public readonly entityId?: string,
  ) {
    super(`API error ${status}: ${code}`);
  }
}

// ─── Base fetch ──────────────────────────────────────────────────────────────

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const res = await fetch(path, {
    method,
    credentials: "include",
    headers: body != null ? { "Content-Type": "application/json" } : undefined,
    body: body != null ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    let code = "unknown_error";
    let entityId: string | undefined;
    try {
      const json = (await res.json()) as { code?: string; entityId?: string };
      code = json.code ?? code;
      entityId = json.entityId;
    } catch {
      // ignore parse failure
    }
    throw new ApiResponseError(res.status, code, entityId);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

const get = <T>(path: string) => request<T>("GET", path);
const post = <T>(path: string, body?: unknown) => request<T>("POST", path, body);
const del = <T>(path: string) => request<T>("DELETE", path);

function toQueryString(params: Record<string, string | number | boolean | (string | number | boolean)[] | null | undefined>) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value == null) continue;
    if (Array.isArray(value)) {
      for (const item of value) search.append(key, String(item));
      continue;
    }
    search.set(key, String(value));
  }
  const query = search.toString();
  return query ? `?${query}` : "";
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

export const authApi = {
  me: () => get<AuthStatus>("/api/admin/auth/me"),
  login: (body: LoginRequest) => post<AuthStatus>("/api/admin/auth/login", body),
  logout: () => post<AuthStatus>("/api/admin/auth/logout"),
};

export const setupApi = {
  status: () => get<SetupStatus>("/api/setup/status"),
  options: () => get<SetupOptions>("/api/setup/options"),
  complete: (body: SetupCompleteRequest) => post<SetupStatus>("/api/setup/complete", body),
};

// ─── Tracked addresses ───────────────────────────────────────────────────────

export const addressApi = {
  list: (includeTombstoned = false) =>
    get<TrackedAddressListItem[]>(
      `/api/admin/tracked/addresses?includeTombstoned=${includeTombstoned}`,
    ),
  detail: (address: string) =>
    get<TrackedAddressDetail>(`/api/admin/tracked/address/${encodeURIComponent(address)}`),
  untrack: (address: string) =>
    del<UntrackResult>(`/api/admin/tracked/address/${encodeURIComponent(address)}`),
  state: (address: string, tokenIds: string[] = []) =>
    get<AddressStateResponse>(
      `/api/address/${encodeURIComponent(address)}/state${toQueryString({ tokenIds })}`,
    ),
  balances: (address: string, tokenIds: string[] = []) =>
    get<BalanceDto[]>(
      `/api/address/${encodeURIComponent(address)}/balances${toQueryString({ tokenIds })}`,
    ),
  utxos: (address: string, tokenId?: string | null, satoshis?: number | null) =>
    get<GetUtxoSetResponse>(
      `/api/address/${encodeURIComponent(address)}/utxos${toQueryString({
        tokenId: tokenId ?? undefined,
        satoshis: satoshis ?? undefined,
      })}`,
    ),
  history: (
    address: string,
    options: {
      tokenIds?: string[];
      desc?: boolean;
      skipZeroBalance?: boolean;
      acceptPartialHistory?: boolean;
      skip?: number;
      take?: number;
    } = {},
  ) =>
    get<AddressHistoryResponse>(
      `/api/address/${encodeURIComponent(address)}/history${toQueryString({
        tokenIds: options.tokenIds ?? [],
        desc: options.desc ?? true,
        skipZeroBalance: options.skipZeroBalance ?? false,
        acceptPartialHistory: options.acceptPartialHistory ?? true,
        skip: options.skip ?? 0,
        take: options.take ?? 1,
      })}`,
    ),
  manage: (body: AddressManageRequest) =>
    post<TrackedAddressDetail>("/api/admin/manage/address", body),
  upgradeHistory: (address: string) =>
    post<unknown>(`/api/admin/manage/address/${encodeURIComponent(address)}/history/full`),
};

// ─── Tracked tokens ──────────────────────────────────────────────────────────

export const tokenApi = {
  list: (includeTombstoned = false) =>
    get<TrackedTokenListItem[]>(
      `/api/admin/tracked/tokens?includeTombstoned=${includeTombstoned}`,
    ),
  detail: (tokenId: string) =>
    get<TrackedTokenDetail>(`/api/admin/tracked/token/${encodeURIComponent(tokenId)}`),
  untrack: (tokenId: string) =>
    del<UntrackResult>(`/api/admin/tracked/token/${encodeURIComponent(tokenId)}`),
  state: (tokenId: string) =>
    get<TokenStateResponse>(`/api/token/${encodeURIComponent(tokenId)}/state`),
  balances: (tokenId: string) =>
    get<BalanceDto[]>(`/api/token/${encodeURIComponent(tokenId)}/balances`),
  utxos: (tokenId: string) =>
    get<GetUtxoSetResponse>(`/api/token/${encodeURIComponent(tokenId)}/utxos`),
  history: (
    tokenId: string,
    options: {
      skip?: number;
      take?: number;
      desc?: boolean;
      acceptPartialHistory?: boolean;
    } = {},
  ) =>
    get<TokenHistoryResponse>(
      `/api/token/${encodeURIComponent(tokenId)}/history${toQueryString({
        skip: options.skip ?? 0,
        take: options.take ?? 1,
        desc: options.desc ?? true,
        acceptPartialHistory: options.acceptPartialHistory ?? true,
      })}`,
    ),
  manage: (body: TokenManageRequest) =>
    post<TrackedTokenDetail>("/api/admin/manage/stas-token", body),
  upgradeHistory: (tokenId: string, body: TokenHistoryUpgradeRequest) =>
    post<unknown>(
      `/api/admin/manage/stas-token/${encodeURIComponent(tokenId)}/history/full`,
      body,
    ),
};

// ─── Dashboard & findings ─────────────────────────────────────────────────────

export const dashboardApi = {
  summary: () => get<DashboardSummary>("/api/admin/dashboard/summary"),
  syncStatus: () => get<SyncStatusResponse>("/api/admin/blockchain/sync-status"),
  cacheStatus: () => get<unknown>("/api/admin/cache/status"),
  storageStatus: () => get<unknown>("/api/admin/storage/status"),
};

export const providersApi = {
  get: () => get<AdminProvidersResponse>("/api/admin/providers"),
  updateConfig: (body: AdminProviderConfigUpdateRequest) =>
    request<AdminProvidersResponse>("PUT", "/api/admin/providers/config", body),
  resetConfig: () =>
    request<AdminProvidersResponse>("DELETE", "/api/admin/providers/config"),
};

export const findingsApi = {
  list: (take = 100) => get<Finding[]>(`/api/admin/findings?take=${take}`),
};

// ─── Ops (detail/diagnostic) ─────────────────────────────────────────────────

export const opsApi = {
  providers: () => get<ProviderStatusResponse[]>("/api/ops/providers"),
  cache: () => get<unknown>("/api/ops/cache"),
  storage: () => get<unknown>("/api/ops/storage"),
  jungleBusBlockSync: () => get<JungleBusBlockSyncStatusResponse>("/api/ops/junglebus/block-sync"),
  jungleBusChainTipAssurance: () => get<JungleBusChainTipAssuranceResponse>("/api/ops/junglebus/chain-tip-assurance"),
};
