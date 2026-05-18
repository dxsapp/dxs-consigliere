/**
 * Admin REST + SignalR DTOs (S4+). Hand-mirrored from C# sources:
 *   - `Dxs.Consigliere.Controllers.AdminP2pController.P2pHealthDto`
 *   - `Dxs.Consigliere.Data.Models.Metrics.SourceMetricsSnapshot`
 *     + nested `SourceObservationCounters` / `SourceVisibilityCounters`
 *
 * The S3 followup replaces these with `api.generated.ts` once the
 * swagger codegen lands. See `src/admin-ui/contracts/README.md`.
 */

/** Wave 1 + W6 — admin pool health surface. */
export interface P2pHealthDto {
  bound: boolean;
  poolSize: number;
  targetPoolSize: number;
  subnet24Diversity: number;
  activePeers: string[];
  /** W6 inbound stub flag. */
  inboundEnabled: boolean;
}

/** Wave 4 — per-source observation counters. */
export interface SourceObservationCounters {
  invObserved: number;
  matched: number;
  unmatched: number;
  parseError: number;
  rateLimited: number;
  getDataTimeout: number;
  oversizePayload: number;
}

/** Wave 4 — per-source visibility counters. */
export interface SourceVisibilityCounters {
  firstSeen: number;
  onlySaw: number;
  lagBuckets: number[];
}

/** Wave 3 W6 — rebroadcast counters. */
export interface OrphanedTxRebroadcastCounters {
  announced: number;
  skippedNoRaw: number;
  skippedCoinbase: number;
  announceNoReadyPeer: number;
  announceFailed: number;
}

/** Wave 4 admin doc snapshot. */
export interface SourceMetricsSnapshot {
  id: string;
  snapshotUnixMs: number;
  observationCounters: Record<string, SourceObservationCounters>;
  visibilityCounters: Record<string, SourceVisibilityCounters>;
  rebroadcast: OrphanedTxRebroadcastCounters;
  lastDegradedReorgAt: string | null;
}

/** Wave 4 admin response wrapping latest + ordered history. */
export interface SourceMetricsResponse {
  latest: SourceMetricsSnapshot | null;
  history: SourceMetricsSnapshot[];
}

/** Wave 1 hub event mirror, also used by the dashboard's
 *  recent-blocks visualisation. */
export interface BlockTipDto {
  hash: string;
  height: number;
  timestampMs: number;
  prevHash: string;
  headerSize: number;
}

/** Known source identifiers — pinned to the C# `TxObservationSource`
 *  constants. */
export const SOURCE_KEYS = ["p2p", "bitails", "junglebus"] as const;
export type SourceKey = (typeof SOURCE_KEYS)[number];

/**
 * Outgoing transaction lifecycle states — hand-mirrored from
 * `Dxs.Consigliere.Data.Models.P2p.OutgoingTxState`. The 5-stage
 * happy-path drives the Stepper in the entity-detail screens.
 */
export type OutgoingTxState =
  | "Submitted"
  | "Validated"
  | "Dispatching"
  | "PeerAcked"
  | "PeerRelayed"
  | "MempoolSeen"
  | "Mined"
  | "Confirmed"
  | "PolicyInvalid"
  | "InvalidRejected"
  | "ConflictRejected"
  | "EvictedOrDropped"
  | "ObserverUnknown"
  | "Failed";

export const TX_HAPPY_PATH: OutgoingTxState[] = [
  "Validated",
  "Dispatching",
  "PeerRelayed",
  "Mined",
  "Confirmed",
];

export const TX_TERMINAL_STATES: OutgoingTxState[] = [
  "Confirmed",
  "PolicyInvalid",
  "InvalidRejected",
  "ConflictRejected",
  "Failed",
];

export const TX_FAILURE_STATES: OutgoingTxState[] = [
  "PolicyInvalid",
  "InvalidRejected",
  "ConflictRejected",
  "EvictedOrDropped",
  "ObserverUnknown",
  "Failed",
];

export function isTxStateTerminal(s: OutgoingTxState): boolean {
  return TX_TERMINAL_STATES.includes(s);
}

export function isTxStateFailure(s: OutgoingTxState): boolean {
  return TX_FAILURE_STATES.includes(s);
}

/** S5 — tracked-history snapshot embedded in the readiness DTO.
 *  Mirrors `Dxs.Consigliere.Dto.Responses.History.TrackedHistoryStatusResponse`. */
export interface TrackedHistoryStatusResponse {
  historyReadiness: string;
  coverage: TrackedHistoryCoverageResponse | null;
  backfillStatus: TrackedHistoryBackfillStatusResponse | null;
  rootedToken: RootedTokenHistoryStatusResponse | null;
}

export interface TrackedHistoryCoverageResponse {
  mode: string;
  fullCoverage: boolean;
  authoritativeFromBlockHeight: number | null;
  authoritativeFromObservedAt: number | null;
}

export interface TrackedHistoryBackfillStatusResponse {
  status: string;
  requestedAt: number | null;
  startedAt: number | null;
  lastProgressAt: number | null;
  completedAt: number | null;
  itemsScanned: number;
  itemsApplied: number;
  errorCode: string | null;
}

export interface RootedTokenHistoryStatusResponse {
  trustedRoots: string[];
  trustedRootCount: number;
  completedTrustedRootCount: number;
  unknownRootFindingCount: number;
  rootedHistorySecure: boolean;
  blockingUnknownRoot: boolean;
  unknownRootFindings: string[];
}

/** S5 — readiness frame shared by Address + Token detail responses. */
export interface TrackedEntityReadinessResponse {
  tracked: boolean;
  entityType: string;
  entityId: string;
  lifecycleStatus: string;
  readable: boolean;
  authoritative: boolean;
  degraded: boolean;
  lagBlocks: number | null;
  progress: number | null;
  history: TrackedHistoryStatusResponse | null;
}

/** S5 — wire shape; backend only exposes id + satoshis here. */
export interface AdminTrackedTokenBalanceSummaryResponse {
  tokenId: string;
  satoshis: number;
}

export interface AdminTrackedAddressSummaryResponse {
  currentBsvBalanceSatoshis: number;
  totalUtxoCount: number;
  bsvUtxoCount: number;
  tokenUtxoCount: number;
  transactionCount: number;
  firstTransactionAt: number | null;
  firstTransactionBlockHeight: number | null;
  lastTransactionAt: number | null;
  lastTransactionBlockHeight: number | null;
  lastProjectionSequence: number | null;
  tokenBalances: AdminTrackedTokenBalanceSummaryResponse[];
}

export interface AdminTrackedAddressResponse {
  address: string;
  name: string;
  isTombstoned: boolean;
  tombstonedAt: number | null;
  createdAt: number;
  updatedAt: number | null;
  failureReason: string | null;
  integritySafe: boolean | null;
  readiness: TrackedEntityReadinessResponse;
  summary: AdminTrackedAddressSummaryResponse;
}

export interface AdminTrackedTokenSummaryResponse {
  protocolType: string;
  validationStatus: string;
  issuer: string | null;
  redeemAddress: string | null;
  localKnownSupplySatoshis: number | null;
  burnedSatoshis: number | null;
  holderCount: number;
  utxoCount: number;
  transactionCount: number;
  firstTransactionAt: number | null;
  firstTransactionBlockHeight: number | null;
  lastTransactionAt: number | null;
  lastTransactionBlockHeight: number | null;
  lastProjectionSequence: number | null;
}

/** S10 — providers config + catalog (`GET /api/admin/providers`).
 *  Mirrors `Dxs.Consigliere.Dto.Responses.Admin.AdminProvidersResponse`. */
export interface AdminProvidersResponse {
  recommendations: AdminProviderRecommendationsResponse;
  config: AdminProviderConfigResponse;
  providers: AdminProviderCatalogItemResponse[];
}

export interface AdminProviderRecommendationsResponse {
  realtimePrimaryProvider: string | null;
  restPrimaryProvider: string | null;
  rawTxFetchProvider: string | null;
}

export interface AdminProviderConfigResponse {
  static: AdminProviderConfigValuesResponse | null;
  override: AdminProviderConfigValuesResponse | null;
  effective: AdminProviderConfigValuesResponse | null;
  overrideActive: boolean;
  restartRequired: boolean;
  allowedRealtimePrimaryProviders: string[];
  allowedRawTxPrimaryProviders: string[];
  allowedRestPrimaryProviders: string[];
  allowedBitailsTransports: string[];
  updatedAt: number | null;
  updatedBy: string | null;
}

export interface AdminProviderConfigValuesResponse {
  realtimePrimaryProvider: string | null;
  rawTxPrimaryProvider: string | null;
  restPrimaryProvider: string | null;
  bitailsTransport: string | null;
  bitails: AdminBitailsProviderConfigResponse;
  whatsonchain: AdminRestProviderConfigResponse;
  junglebus: AdminJungleBusProviderConfigResponse;
}

export interface AdminBitailsProviderConfigResponse {
  apiKey: string | null;
  baseUrl: string | null;
  websocketBaseUrl: string | null;
  zmqTxUrl: string | null;
  zmqBlockUrl: string | null;
}
export interface AdminRestProviderConfigResponse {
  apiKey: string | null;
  baseUrl: string | null;
}
export interface AdminJungleBusProviderConfigResponse {
  baseUrl: string | null;
  mempoolSubscriptionId: string | null;
  blockSubscriptionId: string | null;
}

export interface AdminProviderCatalogItemResponse {
  providerId: string;
  displayName: string;
  roles: string[];
  supportedCapabilities: string[];
  recommendedFor: string[];
  activeFor: string[];
  status: string;
  description: string;
  missingRequirements: string[];
  helpLinks: AdminProviderLinkResponse[];
}

export interface AdminProviderLinkResponse {
  label: string;
  url: string;
}

/** S8 — peer row from `GET /api/admin/p2p/peers`. The backend
 *  returns a plain object; field names mirrored exactly. */
export interface AdminPeerRow {
  endpoint: string;
  source: string;
  userAgent: string | null;
  protocolVersion: number | null;
  services: number | null;
  successCount: number;
  failCount: number;
  firstSeen: string | null;
  lastSeen: string | null;
  lastConnected: string | null;
  negativeUntil: string | null;
  lastFailureReason: string | null;
  subnet24: string;
}

export interface AdminPeersResponse {
  total: number;
  successful: number;
  failed: number;
  distinctSubnets: number;
  peers: AdminPeerRow[];
}

/** S9 — headers tip + recent (display-order hex, audit A2 H3). */
export interface HeadersTipDto {
  hash: string;
  height: number;
  timestampMs: number;
  prevHash: string;
}

/** S7 — backend `P2pAlertType` enum mirror. Frozen (W6 S3). */
export const P2P_ALERT_TYPES = [
  "PoolSizeBelowThreshold",
  "RelayBackRateBelowThreshold",
  "ReorgDepthExceeded",
  "SourceFirstDropout",
] as const;
export type P2pAlertType = (typeof P2P_ALERT_TYPES)[number];

/** Frozen wire shape for a single alert (`AdminP2pController.P2pAlertEventDto`). */
export interface P2pAlertEventDto {
  id: string;
  alertUnixMs: number;
  /** String name — backend serializes the enum as a string for
   *  forwards-compatibility (master.md handoff table). */
  type: string;
  detail: string;
  context: Record<string, string>;
}

/** Frozen response shape for `GET /api/admin/p2p/alerts`. */
export interface P2pAlertResponse {
  alerts: P2pAlertEventDto[];
}

/** S6 — frozen receipt shape returned by `POST /api/tx/broadcast`.
 *  Mirrors `Dxs.Consigliere.WebSockets.BroadcastReceiptDto`. */
export interface BroadcastReceiptDto {
  txId: string;
  state: string;
  createdAtMs: number;
  failReason: string | null;
}

export interface AdminTrackedTokenResponse {
  tokenId: string;
  symbol: string;
  isTombstoned: boolean;
  tombstonedAt: number | null;
  createdAt: number;
  updatedAt: number | null;
  failureReason: string | null;
  integritySafe: boolean | null;
  readiness: TrackedEntityReadinessResponse;
  summary: AdminTrackedTokenSummaryResponse;
}
