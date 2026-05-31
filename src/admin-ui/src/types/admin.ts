// wave-A4 S3 — the hand-mirrored wire interfaces are gone. Every
// admin REST DTO with a generated schema is now a re-export of
// `components["schemas"][...]`, so a backend DTO rename surfaces as
// a screen-side TS compile error (not just a `contracts:check` CI
// diff). The backing C# DTOs were migrated to `#nullable enable`
// with per-property nullability so the generated types are tight
// (NotNull props lose the `?`).
//
// Two documented exceptions remain hand-mirrored:
//   - `BlockTipDto` — a SignalR push DTO, not a REST endpoint, so
//     Swashbuckle never emits a schema for it.
//   - the `OutgoingTxState` union + `SOURCE_KEYS` / `P2P_ALERT_TYPES`
//     helper literals — string enums, not generated object shapes.
import type { components } from "@/types/api.generated";

/** Wave 1 + W6 — admin pool health surface. */
export type P2pHealthDto = components["schemas"]["P2pHealthDto"];

/** Wave 4 — per-source observation counters. */
export type SourceObservationCounters = components["schemas"]["SourceObservationCounters"];

/** Wave 4 — per-source visibility counters. */
export type SourceVisibilityCounters = components["schemas"]["SourceVisibilityCounters"];

/** Wave 3 W6 — rebroadcast counters. */
export type OrphanedTxRebroadcastCounters = components["schemas"]["OrphanedTxRebroadcastCounters"];

/** Wave 4 admin doc snapshot. */
export type SourceMetricsSnapshot = components["schemas"]["SourceMetricsSnapshot"];

/** Wave 4 admin response wrapping latest + ordered history. */
export type SourceMetricsResponse = components["schemas"]["SourceMetricsResponse"];

/**
 * Wave 1 hub event mirror, also used by the dashboard's
 * recent-blocks visualisation. HAND-MIRRORED EXCEPTION: `BlockTipDto`
 * is a SignalR push payload (`Dxs.Consigliere.WebSockets.BlockTipDto`),
 * not a REST response, so Swashbuckle emits no schema for it. Kept
 * as a literal interface by design.
 */
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
 * NOT a generated object shape — a string enum, so it stays a
 * union literal helper.
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
export type TrackedHistoryStatusResponse = components["schemas"]["TrackedHistoryStatusResponse"];
export type TrackedHistoryCoverageResponse = components["schemas"]["TrackedHistoryCoverageResponse"];
export type TrackedHistoryBackfillStatusResponse =
  components["schemas"]["TrackedHistoryBackfillStatusResponse"];
export type RootedTokenHistoryStatusResponse =
  components["schemas"]["RootedTokenHistoryStatusResponse"];

/** S5 — readiness frame shared by Address + Token detail responses. */
export type TrackedEntityReadinessResponse = components["schemas"]["TrackedEntityReadinessResponse"];

/** S5 — wire shape; backend only exposes id + satoshis here. */
export type AdminTrackedTokenBalanceSummaryResponse =
  components["schemas"]["AdminTrackedTokenBalanceSummaryResponse"];

export type AdminTrackedAddressSummaryResponse =
  components["schemas"]["AdminTrackedAddressSummaryResponse"];

export type AdminTrackedAddressResponse = components["schemas"]["AdminTrackedAddressResponse"];

export type AdminTrackedTokenSummaryResponse =
  components["schemas"]["AdminTrackedTokenSummaryResponse"];

export type AdminTrackedTokenResponse = components["schemas"]["AdminTrackedTokenResponse"];

/** Track-a-new-entity request bodies posted to the collection endpoints
 *  (`POST /api/admin/tracked/addresses` / `.../tokens`). */
export type AdminTrackAddressRequest = components["schemas"]["AdminTrackAddressRequest"];
export type AdminTrackTokenRequest = components["schemas"]["AdminTrackTokenRequest"];

/** S10 — setup wizard status (`GET /api/setup/status`). */
export type SetupStatusResponse = components["schemas"]["SetupStatusResponse"];

// ── setup wizard wire DTOs (now generated re-exports) ───────────
export type SetupDefaultsResponse = components["schemas"]["SetupDefaultsResponse"];
export type SetupAllowedOptionsResponse = components["schemas"]["SetupAllowedOptionsResponse"];
export type SetupJungleBusBlockSyncDefaultsResponse =
  components["schemas"]["SetupJungleBusBlockSyncDefaultsResponse"];
export type SetupBitailsProviderDefaultsResponse =
  components["schemas"]["SetupBitailsProviderDefaultsResponse"];
export type SetupRestProviderDefaultsResponse =
  components["schemas"]["SetupRestProviderDefaultsResponse"];
export type SetupJungleBusProviderDefaultsResponse =
  components["schemas"]["SetupJungleBusProviderDefaultsResponse"];
export type SetupNodeProviderDefaultsResponse =
  components["schemas"]["SetupNodeProviderDefaultsResponse"];
export type SetupProviderFormDefaultsResponse =
  components["schemas"]["SetupProviderFormDefaultsResponse"];
export type SetupOptionsResponse = components["schemas"]["SetupOptionsResponse"];

// Request DTOs — wire shape posted to /api/setup/complete.
export type SetupAdminAccessRequest = components["schemas"]["SetupAdminAccessRequest"];
export type AdminBitailsProviderConfigUpdateRequest =
  components["schemas"]["AdminBitailsProviderConfigUpdateRequest"];
export type AdminRestProviderConfigUpdateRequest =
  components["schemas"]["AdminRestProviderConfigUpdateRequest"];
export type AdminJungleBusProviderConfigUpdateRequest =
  components["schemas"]["AdminJungleBusProviderConfigUpdateRequest"];
export type SetupNodeRealtimeConfigRequest =
  components["schemas"]["SetupNodeRealtimeConfigRequest"];
export type SetupProviderSelectionRequest =
  components["schemas"]["SetupProviderSelectionRequest"];
export type SetupJungleBusBlockSyncRequest =
  components["schemas"]["SetupJungleBusBlockSyncRequest"];
export type SetupCompleteRequest = components["schemas"]["SetupCompleteRequest"];

/** S10 — providers config + catalog (`GET /api/admin/providers`). */
export type AdminProvidersResponse = components["schemas"]["AdminProvidersResponse"];
export type AdminProviderRecommendationsResponse =
  components["schemas"]["AdminProviderRecommendationsResponse"];
export type AdminProviderConfigResponse = components["schemas"]["AdminProviderConfigResponse"];
export type AdminProviderConfigValuesResponse =
  components["schemas"]["AdminProviderConfigValuesResponse"];
export type AdminBitailsProviderConfigResponse =
  components["schemas"]["AdminBitailsProviderConfigResponse"];
export type AdminRestProviderConfigResponse =
  components["schemas"]["AdminRestProviderConfigResponse"];
export type AdminJungleBusProviderConfigResponse =
  components["schemas"]["AdminJungleBusProviderConfigResponse"];
export type AdminProviderCatalogItemResponse =
  components["schemas"]["AdminProviderCatalogItemResponse"];
export type AdminProviderLinkResponse = components["schemas"]["AdminProviderLinkResponse"];

/** S8 — peer row + summary from `GET /api/admin/p2p/peers`. wave-A4
 *  S3 sealed the previously-anonymous response into a real DTO. */
export type AdminPeerRow = components["schemas"]["AdminPeerRow"];
export type AdminPeersResponse = components["schemas"]["AdminPeersResponse"];

/** S9 — headers tip + recent (display-order hex, audit A2 H3). */
export type HeadersTipDto = components["schemas"]["HeadersTipDto"];

/** S7 — backend `P2pAlertType` enum mirror. Frozen (W6 S3). NOT a
 *  generated object shape — a string enum helper literal. */
export const P2P_ALERT_TYPES = [
  "PoolSizeBelowThreshold",
  "RelayBackRateBelowThreshold",
  "ReorgDepthExceeded",
  "SourceFirstDropout",
] as const;
export type P2pAlertType = (typeof P2P_ALERT_TYPES)[number];

/** Frozen wire shape for a single alert. */
export type P2pAlertEventDto = components["schemas"]["P2pAlertEventDto"];

/** Frozen response shape for `GET /api/admin/p2p/alerts`. */
export type P2pAlertResponse = components["schemas"]["P2pAlertResponse"];

/** S6 — frozen receipt shape returned by `POST /api/tx/broadcast`. */
export type BroadcastReceiptDto = components["schemas"]["BroadcastReceiptDto"];

/** Broadcast inspector — per-source public-explorer sighting check. */
export type ExternalSightingResponse = components["schemas"]["ExternalSightingResponse"];

/** tx-lab S1 — frozen wire shapes for the lab UTXO lookup
 *  (`GET /api/address/{address}/utxos`). */
export type GetUtxoSetResponse = components["schemas"]["GetUtxoSetResponse"];
export type UtxoDto = components["schemas"]["UtxoDto"];

/** wave-A3 S6 — audit-log surface (first DTOs migrated onto codegen). */
export type AdminAuditLogEntryResponse = components["schemas"]["AdminAuditLogEntryResponse"];
export type AdminAuditLogResponse = components["schemas"]["AdminAuditLogResponse"];
