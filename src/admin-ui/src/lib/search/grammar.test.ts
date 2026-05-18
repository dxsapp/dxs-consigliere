import { describe, expect, it } from "vitest";
import { entityToPath, parseSearchQuery } from "./grammar";

describe("parseSearchQuery", () => {
  it("empty / whitespace → empty", () => {
    expect(parseSearchQuery("")).toEqual({ kind: "empty" });
    expect(parseSearchQuery("   ")).toEqual({ kind: "empty" });
    expect(parseSearchQuery("\t\n")).toEqual({ kind: "empty" });
  });

  it("64-hex → ambiguous (tx first, block-hash alt)", () => {
    const sample = "a".repeat(64);
    const res = parseSearchQuery(sample);
    expect(res.kind).toBe("ambiguous");
    if (res.kind === "ambiguous") {
      expect(res.raw).toBe(sample);
      expect(res.candidates[0]).toEqual({ kind: "tx", txid: sample });
      expect(res.candidates[1]).toEqual({ kind: "block-hash", hash: sample });
    }
  });

  it("64-hex with mixed case lowercases canonical form", () => {
    const mixed = "AbCdEf" + "0".repeat(58);
    const res = parseSearchQuery(mixed);
    expect(res.kind).toBe("ambiguous");
    if (res.kind === "ambiguous") {
      expect(res.candidates[0]).toEqual({ kind: "tx", txid: mixed.toLowerCase() });
    }
  });

  it("non-64-length hex is NOT ambiguous (falls through to token)", () => {
    expect(parseSearchQuery("abcdef")).toEqual({ kind: "token", tokenId: "abcdef" });
    // 63 hex chars
    expect(parseSearchQuery("a".repeat(63)).kind).toBe("token");
  });

  it("positive integer → block-height", () => {
    expect(parseSearchQuery("0")).toEqual({ kind: "block-height", height: 0 });
    expect(parseSearchQuery("123")).toEqual({ kind: "block-height", height: 123 });
    expect(parseSearchQuery("850123")).toEqual({ kind: "block-height", height: 850123 });
  });

  it("non-positive / non-integer → not block-height", () => {
    // Leading + is consumed by parseInt but the regex POSITIVE_INT
    // rejects it, so fallthrough to token.
    expect(parseSearchQuery("-1").kind).toBe("token");
    expect(parseSearchQuery("1.5").kind).toBe("token");
    expect(parseSearchQuery("1e3").kind).toBe("token");
  });

  it("base58-shaped P2PKH address → address", () => {
    // Satoshi's address (1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa).
    const addr = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    expect(parseSearchQuery(addr)).toEqual({ kind: "address", address: addr });
  });

  it("base58-shaped P2SH address (starts with 3) → address", () => {
    const addr = "3J98t1WpEZ73CNmQviecrnyiWrnqRhWNLy";
    expect(parseSearchQuery(addr)).toEqual({ kind: "address", address: addr });
  });

  it("base58 strings that don't look address-shaped fall to token", () => {
    // Starts with a non-{1,3} character → not an address; still
    // base58-alphabet-valid → token fallback.
    expect(parseSearchQuery("BTCpool42").kind).toBe("token");
  });

  it("arbitrary identifiers (token slugs) → token", () => {
    expect(parseSearchQuery("DSTAS-protocol-id-99")).toEqual({
      kind: "token",
      tokenId: "DSTAS-protocol-id-99",
    });
  });
});

describe("entityToPath", () => {
  it("routes each resolved kind to the right path", () => {
    expect(entityToPath({ kind: "tx", txid: "aabb" })).toBe("/transactions/aabb");
    expect(entityToPath({ kind: "block-hash", hash: "cc" })).toBe("/headers?hash=cc");
    expect(entityToPath({ kind: "block-height", height: 42 })).toBe("/headers?height=42");
    expect(entityToPath({ kind: "address", address: "1abc" })).toBe("/addresses/1abc");
    expect(entityToPath({ kind: "token", tokenId: "tok-1" })).toBe("/tokens/tok-1");
  });

  it("returns null for ambiguous + empty (UI renders 'did you mean' instead)", () => {
    expect(
      entityToPath({
        kind: "ambiguous",
        raw: "aa",
        candidates: [],
      })
    ).toBeNull();
    expect(entityToPath({ kind: "empty" })).toBeNull();
  });

  it("URL-encodes path segments to handle special characters", () => {
    expect(entityToPath({ kind: "token", tokenId: "with space" })).toBe("/tokens/with%20space");
  });
});
