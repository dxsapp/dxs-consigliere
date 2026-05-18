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
