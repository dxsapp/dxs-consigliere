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

// ─── Shared read models ──────────────────────────────────────────────────────

export interface BalanceDto {
  address: string;
  tokenId: string | null;
  satoshis: number;
}

export interface UtxoDto {
  id: string;
  txId: string;
  vout: number;
  address: string;
  tokenId: string | null;
  satoshis: number;
  scriptPubKey: string;
  scriptType: string;
}

export interface GetUtxoSetResponse {
  utxoSet: UtxoDto[];
}

export interface AddressStateResponse {
  address: string;
  balances: BalanceDto[];
  utxoSet: UtxoDto[];
}

export interface AddressHistoryItem {
  address: string;
  tokenId: string | null;
  txId: string;
  timestamp: number;
  height: number;
  validStasTx: boolean;
  spentSatoshis: number;
  receivedSatoshis: number;
  balanceSatoshis: number;
  txFeeSatoshis: number;
  note: string | null;
  fromAddresses: string[];
  toAddresses: string[];
}

export interface AddressHistoryResponse {
  history: AddressHistoryItem[];
  totalCount: number;
  historyStatus: TrackedHistoryStatus;
}

export interface TokenStateResponse {
  tokenId: string;
  protocolType: string | null;
  protocolVersion: string | null;
  issuanceKnown: boolean;
  validationStatus: string | null;
  issuer: string | null;
  redeemAddress: string | null;
  totalKnownSupply: number | null;
  burnedSatoshis: number | null;
  lastIndexedHeight: number | null;
}

export interface TokenHistoryItem {
  tokenId: string;
  txId: string;
  timestamp: number;
  height: number;
  receivedSatoshis: number;
  spentSatoshis: number;
  balanceDeltaSatoshis: number;
  isIssue: boolean;
  isRedeem: boolean;
  validationStatus: string;
  protocolType: string;
  confirmedBlockHash: string;
}

export interface TokenHistoryResponse {
  tokenId: string;
  history: TokenHistoryItem[];
  totalCount: number;
  historyStatus: TrackedHistoryStatus;
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
  balanceSatoshis?: number | null;
  tokenBalanceSatoshis?: number | null;
  tokenBalanceCount?: number | null;
  utxoCount?: number | null;
  transactionCount?: number | null;
  firstTransactionAt?: number | null;
  firstTransactionHeight?: number | null;
  lastTransactionAt?: number | null;
  lastTransactionHeight?: number | null;
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
  protocolType?: string | null;
  protocolVersion?: string | null;
  issuanceKnown?: boolean | null;
  validationStatus?: string | null;
  issuer?: string | null;
  redeemAddress?: string | null;
  totalKnownSupply?: number | null;
  burnedSatoshis?: number | null;
  lastIndexedHeight?: number | null;
  holderCount?: number | null;
  utxoCount?: number | null;
  transactionCount?: number | null;
  firstTransactionAt?: number | null;
  firstTransactionHeight?: number | null;
  lastTransactionAt?: number | null;
  lastTransactionHeight?: number | null;
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

export interface ProviderCapabilityStatusResponse {
  enabled: boolean;
  healthy: boolean;
  degraded: boolean;
  lastSuccessAt: string | null;
  lastErrorAt: string | null;
  lastErrorCode: string | null;
  rateLimitState: RateLimitStateResponse | null;
  active: boolean;
  lagBlocks?: number | null;
  observedHeight?: number | null;
  observedAt?: string | null;
  lastSyncRequestAt?: string | null;
}

export interface RateLimitStateResponse {
  limited: boolean;
  remaining: number | null;
  resetAt: string | null;
  scope: string | null;
  sourceHint: string | null;
}

export interface ProviderStatusResponse {
  provider: string;
  enabled: boolean;
  configured: boolean;
  roles: string[];
  healthy: boolean;
  degraded: boolean;
  lastSuccessAt: string | null;
  lastErrorAt: string | null;
  lastErrorCode: string | null;
  rateLimitState: RateLimitStateResponse | null;
  capabilities: Record<string, ProviderCapabilityStatusResponse>;
  observedHeight?: number | null;
  lagBlocks?: number | null;
  observedAt?: string | null;
  lastControlMessageAt?: string | null;
}

export interface JungleBusBlockSyncStatusResponse {
  primary: boolean;
  configured: boolean;
  healthy: boolean;
  degraded: boolean;
  unavailableReason: string | null;
  baseUrl: string | null;
  blockSubscriptionIdConfigured: boolean;
  lastObservedBlockHeight: number | null;
  highestKnownLocalBlockHeight: number | null;
  lagBlocks: number | null;
  lastControlMessageAt: number | null;
  lastControlCode: number | null;
  lastControlStatus: string | null;
  lastControlMessage: string | null;
  lastScheduledAt: number | null;
  lastScheduledFromHeight: number | null;
  lastScheduledToHeight: number | null;
  lastProcessedAt: number | null;
  lastProcessedBlockHeight: number | null;
  lastRequestId: string | null;
  lastError: string | null;
  lastErrorAt: number | null;
}

export interface JungleBusChainTipAssuranceResponse {
  primary: boolean;
  configured: boolean;
  state: "healthy" | "catching_up" | "stalled_control_flow" | "stalled_local_progress" | "unavailable" | (string & {});
  assuranceMode: "single_source" | "cross_checked" | "unavailable" | (string & {});
  singleSourceAssurance: boolean;
  secondaryCrossCheckAvailable: boolean;
  controlFlowStalled: boolean;
  localProgressStalled: boolean;
  unavailableReason: string | null;
  note: string | null;
  lastObservedBlockHeight: number | null;
  highestKnownLocalBlockHeight: number | null;
  lagBlocks: number | null;
  lastObservedMovementAt: number | null;
  lastObservedMovementHeight: number | null;
  lastLocalProgressAt: number | null;
  lastLocalProgressHeight: number | null;
  lastControlMessageAt: number | null;
  lastScheduledAt: number | null;
  lastProcessedAt: number | null;
  lastError: string | null;
  lastErrorAt: number | null;
  controlFlowStaleAfterSeconds: number;
  localProgressStaleAfterSeconds: number;
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
  blockSync: {
    baseUrl: string;
    blockSubscriptionId: string;
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
  blockSync: {
    baseUrl: string;
    blockSubscriptionId: string;
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

export interface SyncStatusResponse {
  height: number;
  isSynced: boolean;
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
