// ─── Auth ─────────────────────────────────────────────────────────────────────

export interface AuthStatus {
  setupRequired: boolean;
  enabled: boolean;
  authenticated: boolean;
  mode: "cookie" | "disabled";
  username?: string;
  sessionTtlMinutes?: number;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface ApiError {
  code: string;
  entityId?: string;
}

// ─── Readiness / history ─────────────────────────────────────────────────────

export interface TrackedHistoryCoverage {
  mode: string;
  fullCoverage: boolean;
  authoritativeFromBlockHeight?: number | null;
  authoritativeFromObservedAt?: number | null;
}

export interface TrackedHistoryBackfillStatus {
  status: string;
  requestedAt?: number | null;
  startedAt?: number | null;
  lastProgressAt?: number | null;
  completedAt?: number | null;
  itemsScanned?: number;
  itemsApplied?: number;
  errorCode?: string | null;
}

export interface RootedTokenHistoryStatus {
  trustedRoots: string[];
  trustedRootCount: number;
  completedTrustedRootCount: number;
  unknownRootFindingCount: number;
  rootedHistorySecure: boolean;
  blockingUnknownRoot: boolean;
  unknownRootFindings: string[];
}

export interface TrackedHistoryStatus {
  historyReadiness: string;
  coverage: TrackedHistoryCoverage;
  backfillStatus?: TrackedHistoryBackfillStatus | null;
  rootedToken?: RootedTokenHistoryStatus | null;
}

export interface TrackedEntityReadiness {
  tracked: boolean;
  entityType: string;
  entityId: string;
  lifecycleStatus: string;
  readable: boolean;
  authoritative: boolean;
  degraded: boolean;
  lagBlocks?: number | null;
  progress?: number | null;
  history?: TrackedHistoryStatus | null;
}

// ─── Tracked Addresses ───────────────────────────────────────────────────────

export interface TrackedAddressListItem {
  address: string;
  name: string | null;
  isTombstoned: boolean;
  tombstonedAt: number | null;
  createdAt: number;
  updatedAt: number | null;
  failureReason: string | null;
  integritySafe: boolean | null;
  readiness: TrackedEntityReadiness;
}

export interface TrackedAddressDetail extends TrackedAddressListItem {
  [key: string]: unknown;
}

// ─── Tracked Tokens ──────────────────────────────────────────────────────────

export interface TrackedTokenListItem {
  tokenId: string;
  symbol: string | null;
  isTombstoned: boolean;
  tombstonedAt: number | null;
  createdAt: number;
  updatedAt: number | null;
  failureReason: string | null;
  integritySafe: boolean | null;
  readiness: TrackedEntityReadiness;
}

export interface TrackedTokenDetail extends TrackedTokenListItem {
  [key: string]: unknown;
}

// ─── Untrack result ──────────────────────────────────────────────────────────

export interface UntrackResult {
  entityType: string;
  entityId: string;
  code: "untracked";
  tombstoned: true;
  tombstonedAt: number;
}

// ─── Findings ────────────────────────────────────────────────────────────────

export type FindingSeverity = "error" | "warning" | (string & {});

export interface Finding {
  entityType: string;
  entityId: string;
  code: string;
  severity: FindingSeverity;
  message: string;
  observedAt: number | null;
}

// ─── Dashboard ───────────────────────────────────────────────────────────────

export interface DashboardSummary {
  activeAddressCount: number;
  activeTokenCount: number;
  tombstonedAddressCount: number;
  tombstonedTokenCount: number;
  degradedAddressCount: number;
  degradedTokenCount: number;
  backfillingAddressCount: number;
  backfillingTokenCount: number;
  fullHistoryLiveAddressCount: number;
  fullHistoryLiveTokenCount: number;
  unknownRootFindingCount: number;
  blockingUnknownRootTokenCount: number;
  failureCount: number;
}

// ─── Runtime source policy ───────────────────────────────────────────────────

export interface AdminRealtimeSourcePolicyValues {
  primaryRealtimeSource: string;
  fallbackSources: string[];
  bitailsTransport: string;
}

export interface AdminRealtimeSourcePolicy {
  static: AdminRealtimeSourcePolicyValues;
  override: AdminRealtimeSourcePolicyValues | null;
  effective: AdminRealtimeSourcePolicyValues;
  overrideActive: boolean;
  restartRequired: boolean;
  allowedPrimarySources: string[];
  allowedBitailsTransports: string[];
  updatedAt: number | null;
  updatedBy: string | null;
}

export interface AdminRuntimeSources {
  realtimePolicy: AdminRealtimeSourcePolicy;
}

export interface AdminRealtimeSourcePolicyUpdateRequest {
  primaryRealtimeSource: string;
  bitailsTransport: string;
}

// ─── Providers page ──────────────────────────────────────────────────────────

export interface AdminProviderHelpLink {
  label: string;
  url: string;
}

export interface AdminBitailsProviderConfig {
  apiKey: string;
  baseUrl: string;
  websocketBaseUrl: string;
  zmqTxUrl: string;
  zmqBlockUrl: string;
}

export interface AdminRestProviderConfig {
  apiKey: string;
  baseUrl: string;
}

export interface AdminJungleBusProviderConfig {
  baseUrl: string;
  mempoolSubscriptionId: string;
  blockSubscriptionId: string;
}

export interface AdminProviderConfigValues {
  realtimePrimaryProvider: string;
  rawTxPrimaryProvider: string;
  restPrimaryProvider: string;
  bitailsTransport: string;
  bitails: AdminBitailsProviderConfig;
  whatsonchain: AdminRestProviderConfig;
  junglebus: AdminJungleBusProviderConfig;
}

export interface AdminProviderCatalogItem {
  providerId: string;
  displayName: string;
  roles: string[];
  supportedCapabilities: string[];
  recommendedFor: string[];
  activeFor: string[];
  status: string;
  description: string;
  missingRequirements: string[];
  helpLinks: AdminProviderHelpLink[];
}

export interface AdminProvidersResponse {
  recommendations: {
    realtimePrimaryProvider: string;
    restPrimaryProvider: string;
    rawTxFetchProvider: string;
  };
  config: {
    static: AdminProviderConfigValues;
    override: AdminProviderConfigValues | null;
    effective: AdminProviderConfigValues;
    overrideActive: boolean;
    restartRequired: boolean;
    allowedRealtimePrimaryProviders: string[];
    allowedRawTxPrimaryProviders: string[];
    allowedRestPrimaryProviders: string[];
    allowedBitailsTransports: string[];
    updatedAt: number | null;
    updatedBy: string | null;
  };
  providers: AdminProviderCatalogItem[];
}

export interface AdminProviderConfigUpdateRequest {
  realtimePrimaryProvider: string;
  rawTxPrimaryProvider: string;
  restPrimaryProvider: string;
  bitailsTransport: string;
  bitails: AdminBitailsProviderConfig;
  whatsonchain: AdminRestProviderConfig;
  junglebus: AdminJungleBusProviderConfig;
}

// ─── Setup wizard ────────────────────────────────────────────────────────────

export interface SetupStatus {
  setupRequired: boolean;
  setupCompleted: boolean;
  adminEnabled: boolean;
  adminUsername?: string;
}

export interface SetupOptions {
  status: SetupStatus;
  defaults: {
    rawTxPrimaryProvider: string;
    restFallbackProvider: string;
    realtimePrimaryProvider: string;
    bitailsTransport: string;
  };
  allowed: {
    rawTxPrimaryProviders: string[];
    restFallbackProviders: string[];
    realtimePrimaryProviders: string[];
    bitailsTransports: string[];
  };
  providerConfig: {
    bitails: AdminBitailsProviderConfig;
    whatsonchain: AdminRestProviderConfig;
    junglebus: AdminJungleBusProviderConfig & { apiKey?: string };
    node: {
      zmqTxUrl: string;
      zmqBlockUrl: string;
    };
  };
}

export interface SetupCompleteRequest {
  admin: {
    enabled: boolean;
    username: string;
    password: string;
  };
  providers: {
    rawTxPrimaryProvider: string;
    restFallbackProvider: string;
    realtimePrimaryProvider: string;
    bitailsTransport: string;
    bitails: AdminBitailsProviderConfig;
    whatsonchain: AdminRestProviderConfig;
    junglebus: AdminJungleBusProviderConfig;
    node: {
      zmqTxUrl: string;
      zmqBlockUrl: string;
    };
  };
}

// ─── Manage ──────────────────────────────────────────────────────────────────

export type HistoryPolicyMode = "forward_only" | "full_history";

export interface AddressManageRequest {
  address: string;
  name?: string;
  historyPolicy: { mode: HistoryPolicyMode };
}

export interface TokenManageRequest {
  tokenId: string;
  symbol?: string;
  historyPolicy: { mode: HistoryPolicyMode };
  tokenHistoryPolicy?: { trustedRoots: string[] };
}

export interface TokenHistoryUpgradeRequest {
  trustedRoots: string[];
}
