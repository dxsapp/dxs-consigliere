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

/** S5 — tracked-history snapshot embedded in the readiness DTO. */
export interface TrackedHistoryStatusResponse {
  status: string;
  rangeStart?: number | null;
  rangeEnd?: number | null;
  lastCheckpoint?: number | null;
  authoritativeSeq?: number | null;
  pendingCount?: number;
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

export interface AdminTrackedTokenBalanceSummaryResponse {
  tokenId: string;
  symbol: string;
  balanceSatoshis: number;
  utxoCount: number;
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
