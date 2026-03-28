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
  AdminRuntimeSources,
  AdminRealtimeSourcePolicyUpdateRequest,
  AdminProvidersResponse,
  AdminProviderConfigUpdateRequest,
  AddressManageRequest,
  TokenManageRequest,
  TokenHistoryUpgradeRequest,
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

// ─── Auth ─────────────────────────────────────────────────────────────────────

export const authApi = {
  me: () => get<AuthStatus>("/api/admin/auth/me"),
  login: (body: LoginRequest) => post<AuthStatus>("/api/admin/auth/login", body),
  logout: () => post<AuthStatus>("/api/admin/auth/logout"),
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
  syncStatus: () => get<unknown>("/api/admin/blockchain/sync-status"),
  cacheStatus: () => get<unknown>("/api/admin/cache/status"),
  storageStatus: () => get<unknown>("/api/admin/storage/status"),
};

export const runtimeSourcesApi = {
  get: () => get<AdminRuntimeSources>("/api/admin/runtime/sources"),
  updateRealtimePolicy: (body: AdminRealtimeSourcePolicyUpdateRequest) =>
    request<AdminRuntimeSources>("PUT", "/api/admin/runtime/sources/realtime-policy", body),
  resetRealtimePolicy: () =>
    request<AdminRuntimeSources>("DELETE", "/api/admin/runtime/sources/realtime-policy"),
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
  providers: () => get<unknown>("/api/ops/providers"),
  cache: () => get<unknown>("/api/ops/cache"),
  storage: () => get<unknown>("/api/ops/storage"),
};
