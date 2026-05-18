/**
 * Smart-search grammar (A1 M6 / Core Rule §14).
 *
 * Pure-logic decision table that maps an operator-typed input
 * string to a routed entity. The header `<Autocomplete freeSolo>`
 * calls `parseSearchQuery` on Enter; the result drives the router.
 *
 * Rules (in order):
 *  1. empty / whitespace                 → `empty`
 *  2. 64-hex                             → `ambiguous` (tx first, block-hash alt)
 *  3. positive integer                   → `block-height`
 *  4. base58 (1-9 A-H J-N P-Z a-k m-z)   → `address`
 *  5. otherwise (non-empty)              → `token` (fallback)
 *
 * Ambiguous results render a "did you mean ..." chip row in the UI;
 * the operator picks tx-vs-block explicitly.
 */

export type SearchEntity =
  | { kind: "empty" }
  | { kind: "tx"; txid: string }
  | { kind: "block-hash"; hash: string }
  | { kind: "block-height"; height: number }
  | { kind: "address"; address: string }
  | { kind: "token"; tokenId: string }
  | { kind: "ambiguous"; candidates: SearchEntity[]; raw: string };

const HEX_64 = /^[0-9a-fA-F]{64}$/;
const POSITIVE_INT = /^\d+$/;
// BSV base58 alphabet excludes 0, O, I, l. Typical addresses are
// 26-35 chars and start with 1 (P2PKH mainnet) or 3 (P2SH).
const BASE58 = /^[1-9A-HJ-NP-Za-km-z]+$/;
const BASE58_LIKELY_ADDR = /^[13][1-9A-HJ-NP-Za-km-z]{25,34}$/;

export function parseSearchQuery(raw: string): SearchEntity {
  const trimmed = raw.trim();
  if (trimmed.length === 0) return { kind: "empty" };

  if (HEX_64.test(trimmed)) {
    return {
      kind: "ambiguous",
      raw: trimmed,
      candidates: [
        { kind: "tx", txid: trimmed.toLowerCase() },
        { kind: "block-hash", hash: trimmed.toLowerCase() },
      ],
    };
  }

  if (POSITIVE_INT.test(trimmed)) {
    const height = Number.parseInt(trimmed, 10);
    if (Number.isSafeInteger(height) && height >= 0) {
      return { kind: "block-height", height };
    }
  }

  if (BASE58_LIKELY_ADDR.test(trimmed)) {
    return { kind: "address", address: trimmed };
  }

  // Lone base58 strings that aren't address-shaped fall through to
  // the token fallback below; that's intentional — token ids in our
  // domain are arbitrary identifiers, often non-base58.
  if (BASE58.test(trimmed)) {
    return { kind: "token", tokenId: trimmed };
  }

  // Final fallback: treat as token id (operators sometimes paste
  // human-readable token slugs).
  return { kind: "token", tokenId: trimmed };
}

/**
 * Map a resolved (non-ambiguous, non-empty) entity to its router
 * path. Returns `null` for ambiguous / empty — the UI renders the
 * "did you mean" panel instead of navigating.
 */
export function entityToPath(entity: SearchEntity): string | null {
  switch (entity.kind) {
    case "tx":
      return `/transactions/${entity.txid}`;
    case "block-hash":
      return `/headers?hash=${encodeURIComponent(entity.hash)}`;
    case "block-height":
      return `/headers?height=${entity.height}`;
    case "address":
      return `/addresses/${encodeURIComponent(entity.address)}`;
    case "token":
      return `/tokens/${encodeURIComponent(entity.tokenId)}`;
    case "ambiguous":
    case "empty":
      return null;
  }
}
