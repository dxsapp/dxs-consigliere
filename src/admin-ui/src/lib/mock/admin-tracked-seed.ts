import type {
  AdminTrackedAddressResponse,
  AdminTrackedTokenResponse,
  TrackedHistoryStatusResponse,
} from "@/types/admin";

function readyHistory(now: number): TrackedHistoryStatusResponse {
  return {
    historyReadiness: "Ready",
    coverage: {
      mode: "Full",
      fullCoverage: true,
      authoritativeFromBlockHeight: 850_000,
      authoritativeFromObservedAt: now - 60 * 24 * 60 * 60 * 1000,
    },
    backfillStatus: {
      status: "Completed",
      requestedAt: now - 60 * 60 * 1000,
      startedAt: now - 60 * 60 * 1000,
      lastProgressAt: now - 30 * 60 * 1000,
      completedAt: now - 30 * 60 * 1000,
      itemsScanned: 12_840,
      itemsApplied: 12_840,
      errorCode: null,
    },
    rootedToken: null,
  };
}

/**
 * Detail seeds for the MockAdminClient. Lives in a sibling file
 * imported via `await import()` so the ~0.5 KB of literal data
 * never lands in the cold-load shell — only when the entity-detail
 * pages are actually opened (S3-audit M6 bundle headroom).
 */
export function seedAddress(address: string, now: number): AdminTrackedAddressResponse {
  return {
    address,
    name: "Hot wallet 01",
    isTombstoned: false,
    tombstonedAt: null,
    createdAt: now - 14 * 24 * 60 * 60 * 1000,
    updatedAt: now - 5 * 60 * 1000,
    failureReason: null,
    integritySafe: true,
    readiness: {
      tracked: true,
      entityType: "Address",
      entityId: address,
      lifecycleStatus: "Ready",
      readable: true,
      authoritative: true,
      degraded: false,
      lagBlocks: 0,
      progress: 1,
      history: readyHistory(now),
    },
    summary: {
      currentBsvBalanceSatoshis: 412_500_000,
      totalUtxoCount: 14,
      bsvUtxoCount: 11,
      tokenUtxoCount: 3,
      transactionCount: 87,
      firstTransactionAt: now - 14 * 24 * 60 * 60 * 1000,
      firstTransactionBlockHeight: 901_004,
      lastTransactionAt: now - 30 * 60 * 1000,
      lastTransactionBlockHeight: 902_812,
      lastProjectionSequence: 124_512,
      tokenBalances: [{ tokenId: "tok1", satoshis: 100_000 }],
    },
  };
}

export function seedToken(tokenId: string, now: number): AdminTrackedTokenResponse {
  return {
    tokenId,
    symbol: "DSTAS",
    isTombstoned: false,
    tombstonedAt: null,
    createdAt: now - 60 * 24 * 60 * 60 * 1000,
    updatedAt: now - 2 * 60 * 1000,
    failureReason: null,
    integritySafe: true,
    readiness: {
      tracked: true,
      entityType: "Token",
      entityId: tokenId,
      lifecycleStatus: "Ready",
      readable: true,
      authoritative: true,
      degraded: false,
      lagBlocks: 1,
      progress: 0.998,
      history: readyHistory(now),
    },
    summary: {
      protocolType: "DSTAS",
      validationStatus: "Validated",
      issuer: null,
      redeemAddress: null,
      localKnownSupplySatoshis: 21_000_000,
      burnedSatoshis: 18_400,
      holderCount: 1_842,
      utxoCount: 9_217,
      transactionCount: 12_840,
      firstTransactionAt: now - 60 * 24 * 60 * 60 * 1000,
      firstTransactionBlockHeight: 850_120,
      lastTransactionAt: now - 30 * 1000,
      lastTransactionBlockHeight: 902_815,
      lastProjectionSequence: 412_900,
    },
  };
}
